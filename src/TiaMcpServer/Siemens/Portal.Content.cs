using Siemens.Engineering;
using Microsoft.Extensions.Logging;
using Siemens.Engineering.SW.ExternalSources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TiaMcpServer.Security;

namespace TiaMcpServer.Siemens
{
    public sealed class EngineeringContentPage
    {
        public string SnapshotId { get; set; } = "";
        public string EntityPath { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Representation { get; set; } = "";
        public string ContentSha256 { get; set; } = "";
        public DateTimeOffset CapturedAtUtc { get; set; }
        public int TotalCharacters { get; set; }
        public int Offset { get; set; }
        public int ReturnedCharacters { get; set; }
        public string Content { get; set; } = "";
        public bool HasMore { get; set; }
        public string? NextCursor { get; set; }
        public string Interpretation { get; set; } = "Immutable native content captured without compiling or saving. Later pages belong to this capture, not a fresh project read. Temporary export files are removed after capture.";
    }

    internal sealed class EngineeringContentSnapshot
    {
        internal string Id { get; } = Guid.NewGuid().ToString("N");
        internal string Query { get; set; } = "";
        internal string Path { get; set; } = "";
        internal string Kind { get; set; } = "";
        internal string Representation { get; set; } = "";
        internal string Text { get; set; } = "";
        internal DateTimeOffset CapturedAtUtc { get; } = DateTimeOffset.UtcNow;

        internal EngineeringContentPage Page(int maximumCharacters, string cursor)
        {
            if (maximumCharacters < 256 || maximumCharacters > 16000)
                throw new ArgumentException("maxCharacters must be between 256 and 16000.");
            var scope = "content:" + Id + ":" + Query;
            var offset = new ModelContextProtocol.PageRequest { Cursor = cursor }.GetOffset(scope);
            if (offset > Text.Length || (offset > 0 && offset < Text.Length && char.IsLowSurrogate(Text[offset])))
                throw new ArgumentException("Content cursor is outside this snapshot or splits a Unicode character.");
            var count = Math.Min(maximumCharacters, Text.Length - offset);
            if (count > 0 && offset + count < Text.Length && char.IsHighSurrogate(Text[offset + count - 1])) count--;
            string hash;
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Text))).Replace("-", "").ToLowerInvariant();
            return new EngineeringContentPage
            {
                SnapshotId = Id, EntityPath = Path, Kind = Kind, Representation = Representation,
                ContentSha256 = hash, CapturedAtUtc = CapturedAtUtc, TotalCharacters = Text.Length,
                Offset = offset, ReturnedCharacters = count, Content = Text.Substring(offset, count),
                HasMore = offset + count < Text.Length,
                NextCursor = offset + count < Text.Length ? ModelContextProtocol.PageRequest.CreateCursor(offset + count, scope) : null
            };
        }
    }

    public partial class Portal
    {
        private EngineeringContentSnapshot? _contentSnapshot;
        private ProjectBase? _contentProject;
        private TiaPortal? _contentPortal;

        public object[] GetExternalSources(string softwarePath)
        {
            var software = RequirePlcSoftware(softwarePath);
            return software.ExternalSourceGroup.ExternalSources.OrderBy(s => s.Name, StringComparer.Ordinal)
                .Select(s => (object)new
                {
                    s.Name, Path = "External source files/" + s.Name,
                    Kind = "ExternalSource", ContentReadable = false,
                    Limitation = "This adapter lists native external-source identity. The exact PublicAPI has no implemented source-body reader; read generated blocks through ReadBlockContent in ReadWrite."
                }).ToArray();
        }

        public EngineeringContentPage ReadBlockContent(string softwarePath, string blockPath, int maxCharacters, string cursor, string representation)
        {
            ProfileAccessPolicy.DemandWrite("capture block content through a temporary file export");
            ValidateContentRequest(maxCharacters, representation);
            var query = "block\n" + softwarePath + "\n" + blockPath + "\n" + representation;
            return ReadContent(query, maxCharacters, cursor, () =>
            {
                var block = GetBlock(softwarePath, blockPath) ?? throw new ArgumentException("Block not found. Use GetBlocks and reuse its path.");
                if (block.IsKnowHowProtected) throw new ArgumentException("The block is know-how protected. Its implementation cannot be read by this tool.");
                if (!block.IsConsistent) throw new ArgumentException("The block is inconsistent. Compile and inspect diagnostics before requesting native content; no compilation was performed.");
                return new EngineeringContentSnapshot
                {
                    Query = query, Path = GetBlockPath(block), Kind = "Block", Representation = representation,
                    Text = CaptureContentFile(file =>
                    {
                        if (representation == "SimaticML") block.Export(file, ExportOptions.None);
                        else
                        {
                            if (!(block is IGenerateSource source)) throw new NotSupportedException("Native source generation is unavailable for this block. Request SimaticML instead.");
                            RequirePlcSoftware(softwarePath).ExternalSourceGroup.GenerateSource(new[] { source }, file);
                        }
                    }, representation == "SimaticML" ? ".xml" : block.ProgrammingLanguage.ToString() == "SCL" ? ".scl" : ".awl")
                };
            });
        }

        public EngineeringContentPage ReadTypeContent(string softwarePath, string typePath, int maxCharacters, string cursor)
        {
            ProfileAccessPolicy.DemandWrite("capture type content through a temporary file export");
            ValidateContentRequest(maxCharacters, "SimaticML");
            var query = "type\n" + softwarePath + "\n" + typePath;
            return ReadContent(query, maxCharacters, cursor, () =>
            {
                var type = GetType(softwarePath, typePath) ?? throw new ArgumentException("Type not found. Use GetTypes and reuse its path.");
                if (!type.IsConsistent) throw new ArgumentException("The type is inconsistent. Compile and inspect diagnostics before requesting native content; no compilation was performed.");
                return new EngineeringContentSnapshot
                {
                    Query = query, Path = GetTypePath(type), Kind = "Type", Representation = "SimaticML",
                    Text = CaptureContentFile(file => type.Export(file, ExportOptions.None), ".xml")
                };
            });
        }

        private EngineeringContentPage ReadContent(string query, int maximumCharacters, string cursor, Func<EngineeringContentSnapshot> capture)
        {
            if (IsProjectNull()) throw new ArgumentException("No selected project. Connect and inspect GetState first.");
            if (string.IsNullOrEmpty(cursor))
            {
                _contentSnapshot = null;
                var captured = capture();
                _contentSnapshot = captured;
                _contentProject = _project;
                _contentPortal = _portal;
            }
            if (_contentSnapshot == null || _contentPortal != _portal || !SameProject(_contentProject, _project)
                || !string.Equals(_contentSnapshot.Query, query, StringComparison.Ordinal))
                throw new ArgumentException("Content snapshot expired or belongs to another query/project. Restart without a cursor.");
            return _contentSnapshot.Page(maximumCharacters, cursor);
        }

        internal static void ValidateContentRequest(int maximumCharacters, string representation)
        {
            if (maximumCharacters < 256 || maximumCharacters > 16000)
                throw new ArgumentException("maxCharacters must be between 256 and 16000.");
            if (representation != "SimaticML" && representation != "Source")
                throw new ArgumentException("representation must be SimaticML or Source. Native Source is supported only for source-generatable blocks.");
        }

        private string CaptureContentFile(Action<FileInfo> export, string extension)
        {
            var folder = OutputPathPolicy.ResolveDirectory(Path.Combine(".mcp-content", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(folder);
            folder = OutputPathPolicy.ResolveDirectory(folder);
            var filePath = Path.Combine(folder, "content" + extension);
            var completed = false;
            try
            {
                export(new FileInfo(filePath));
                var info = new FileInfo(filePath);
                if (!info.Exists || info.Length > 4 * 1024 * 1024)
                    throw new ArgumentException("Native content is missing or exceeds the 4 MiB capture limit. Use a selected export workflow for larger content.");
                var content = File.ReadAllText(filePath);
                completed = true;
                return content;
            }
            finally
            {
                // Delete only the exact export we created; never recursively delete a caller-supplied path.
                try
                {
                    if (File.Exists(filePath)) File.Delete(filePath);
                    Directory.Delete(folder, false);
                }
                catch (Exception ex) when (!completed && (ex is IOException || ex is UnauthorizedAccessException))
                {
                    _logger?.LogWarning(ex, "Content export failed and its temporary folder could not be removed: {Folder}", folder);
                }
            }
        }
    }
}
