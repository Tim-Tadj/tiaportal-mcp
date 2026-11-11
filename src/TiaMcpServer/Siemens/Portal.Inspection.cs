using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.HW;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.ExternalSources;
#if !TIA_MCP_V17
using Siemens.Engineering.CrossReference;
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TiaMcpServer.Security;

namespace TiaMcpServer.Siemens
{
    public sealed class BlockExportFailure
    {
        public string? Path { get; set; }
        public string? Name { get; set; }
        public string? Reason { get; set; }
    }

    public sealed class CompileDiagnostic
    {
        public int Index { get; set; }
        public int? ParentIndex { get; set; }
        public string? Severity { get; set; }
        public string? Path { get; set; }
        public string? Description { get; set; }
    }

    public sealed class CompileReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string? State { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public List<CompileDiagnostic> Diagnostics { get; set; } = new List<CompileDiagnostic>();
    }

    public sealed class PlcTagEntry
    {
        public string? TablePath { get; set; }
        public string? Name { get; set; }
        public string? Kind { get; set; }
        public string? DataType { get; set; }
        public string? Address { get; set; }
        public string? Value { get; set; }
    }

    public sealed class PlcTagTableEntry
    {
        public string? Path { get; set; }
        public string? Name { get; set; }
        public int TagCount { get; set; }
        public int ConstantCount { get; set; }
    }

    public sealed class PlcReferenceEntry
    {
        public string? SourcePath { get; set; }
        public string? SourceName { get; set; }
        public string? SourceAddress { get; set; }
        public string? TargetPath { get; set; }
        public string? TargetName { get; set; }
        public string? TargetAddress { get; set; }
        public string? Access { get; set; }
        public string? ReferenceType { get; set; }
        public string? Location { get; set; }
        public string? ReferencedAs { get; set; }
    }

    public partial class Portal
    {
        internal static IEnumerable<string> ChildGroupNames(string groupPath, string rootName)
        {
            var segments = groupPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 0 && string.Equals(segments[0], rootName, StringComparison.OrdinalIgnoreCase)
                ? segments.Skip(1) : segments;
        }

        private CompileReport? _lastCompile;
        private ProjectBase? _compileProject;

        private PlcSoftware RequirePlcSoftware(string softwarePath)
        {
            if (IsProjectNull()) throw new ArgumentException("No project is selected. Connect and check GetState first.");
            return GetSoftwareContainer(softwarePath)?.Software as PlcSoftware
                ?? throw new ArgumentException("PLC software was not found at the supplied path.");
        }

        public CompileReport RememberCompile(string softwarePath, CompilerResult result)
        {
            var report = new CompileReport
            {
                State = result.State.ToString(), ErrorCount = result.ErrorCount, WarningCount = result.WarningCount
            };
            AddCompileMessages(report.Diagnostics, result.Messages, null);
            _lastCompile = report;
            _compileProject = _project;
            return report;
        }

        private static void AddCompileMessages(List<CompileDiagnostic> rows, CompilerResultMessageComposition messages, int? parent)
        {
            foreach (var message in messages)
            {
                var index = rows.Count;
                rows.Add(new CompileDiagnostic
                {
                    Index = index, ParentIndex = parent, Severity = message.State.ToString(),
                    Path = message.Path, Description = message.Description
                });
                AddCompileMessages(rows, message.Messages, index);
            }
        }

        public CompileReport GetCompileReport(string compileId)
        {
            if (IsProjectNull() || !SameProject(_compileProject, _project) || _lastCompile?.Id != compileId)
                throw new ArgumentException("Compile results expired or compileId is invalid. Compile the selected PLC software again.");
            return _lastCompile;
        }

        private static IEnumerable<(PlcTagTable Table, string Path)> TagTables(PlcTagTableGroup group, string prefix)
        {
            foreach (var table in group.TagTables) yield return (table, prefix + "/" + table.Name);
            foreach (var child in group.Groups)
                foreach (var entry in TagTables(child, prefix + "/" + child.Name)) yield return entry;
        }

        public IReadOnlyList<PlcTagTableEntry> GetPlcTagTables(string softwarePath)
        {
            return TagTables(RequirePlcSoftware(softwarePath).TagTableGroup, "PLC tags")
                .Select(t => new PlcTagTableEntry
                {
                    Path = t.Path, Name = t.Table.Name, TagCount = t.Table.Tags.Count,
                    ConstantCount = t.Table.UserConstants.Count
                }).OrderBy(t => t.Path, StringComparer.Ordinal).ToList();
        }

        public string GetTagTableTree(string softwarePath)
        {
            return "\nPLC tags\n" + string.Join("\n", GetPlcTagTables(softwarePath)
                .Select(t => t.Path + " (" + t.TagCount + " tags, " + t.ConstantCount + " constants)")) + "\n";
        }

        public IReadOnlyList<PlcTagEntry> GetPlcTags(string softwarePath, string tablePath)
        {
            var tables = TagTables(RequirePlcSoftware(softwarePath).TagTableGroup, "PLC tags").ToList();
            if (!string.IsNullOrEmpty(tablePath))
            {
                tables = tables.Where(t => t.Path == tablePath).ToList();
                if (tables.Count == 0) throw new ArgumentException("Tag table path was not found. Use GetPlcTagTables first.");
            }
            var rows = new List<PlcTagEntry>();
            foreach (var entry in tables)
            {
                rows.AddRange(entry.Table.Tags.Select(t => new PlcTagEntry
                {
                    TablePath = entry.Path, Name = t.Name, Kind = "Tag", DataType = t.DataTypeName, Address = t.LogicalAddress
                }));
                rows.AddRange(entry.Table.UserConstants.Select(t => new PlcTagEntry
                {
                    TablePath = entry.Path, Name = t.Name, Kind = "Constant", DataType = t.DataTypeName, Value = Convert.ToString(t.Value)
                }));
            }
            return rows.OrderBy(t => t.TablePath, StringComparer.Ordinal).ThenBy(t => t.Kind, StringComparer.Ordinal)
                .ThenBy(t => t.Name, StringComparer.Ordinal).ToList();
        }

        public IReadOnlyList<PlcReferenceEntry> FindReferences(string softwarePath, string search)
        {
            var software = RequirePlcSoftware(softwarePath);
#if TIA_MCP_V17
            throw new NotSupportedException("Native cross-reference inspection requires TIA Portal V18 or V19. V17 PublicAPI does not expose CrossReferenceService.");
#else
            var targets = new List<IEngineeringServiceProvider>();
            Func<string?, bool> matches = text => string.Equals(text, search, StringComparison.OrdinalIgnoreCase);
            foreach (var block in GetBlocks(softwarePath))
            {
                var kind = block is GlobalDB || block is InstanceDB ? "DB" : block.GetType().Name;
                if (matches(block.Name) || matches(GetBlockPath(block)) || matches(kind + block.Number)) targets.Add(block);
            }
            foreach (var table in TagTables(software.TagTableGroup, "PLC tags"))
                foreach (var tag in table.Table.Tags)
                    if (matches(tag.Name) || matches(tag.LogicalAddress) || matches(table.Path + "/" + tag.Name)) targets.Add(tag);
            if (targets.Count == 0)
                throw new ArgumentException("No exact block or tag matched. Supply a block name, canonical block path, DB number such as DB1000, tag name or logical address.");
            if (targets.Count > 1)
                throw new ArgumentException("More than one block or tag matched. Use its canonical path from GetBlocks or GetPlcTags.");
            var service = targets[0].GetService<CrossReferenceService>()
                ?? throw new NotSupportedException("The selected object does not expose CrossReferenceService. No text-search approximation was used.");
            var rows = new List<PlcReferenceEntry>();
            AddReferences(service.GetCrossReferences(CrossReferenceFilter.AllObjects).Sources, rows);
            return rows.OrderBy(t => t.SourcePath, StringComparer.Ordinal)
                .ThenBy(t => t.TargetPath, StringComparer.Ordinal).ThenBy(t => t.Location, StringComparer.Ordinal).ToList();
#endif
        }

#if !TIA_MCP_V17
        private static void AddReferences(SourceObjectComposition sources, List<PlcReferenceEntry> rows)
        {
            foreach (var source in sources)
            {
                foreach (var reference in source.References)
                    foreach (var location in reference.Locations)
                        rows.Add(new PlcReferenceEntry
                        {
                            SourcePath = Convert.ToString(source.Path), SourceName = source.Name, SourceAddress = source.Address,
                            TargetPath = Convert.ToString(reference.Path), TargetName = reference.Name, TargetAddress = reference.Address,
                            Access = location.Access.ToString(), ReferenceType = location.ReferenceType.ToString(),
                            Location = Convert.ToString(location.ReferenceLocation), ReferencedAs = location.ReferencedAsName
                        });
                AddReferences(source.Children, rows);
            }
        }
#endif

        public object ImportBlockFromSource(string softwarePath, string sourcePath, bool acceptBlockReplacement)
        {
            ProfileAccessPolicy.DemandWrite("generate PLC blocks from external source");
            ValidateSourceFile(sourcePath, acceptBlockReplacement);
            var software = RequirePlcSoftware(softwarePath);
            var sourceName = "MCP_" + Guid.NewGuid().ToString("N") + Path.GetExtension(sourcePath);
            var source = software.ExternalSourceGroup.ExternalSources.CreateFromFile(sourceName, Path.GetFullPath(sourcePath));
            try
            {
                var generated = source.GenerateBlocksFromSource(GenerateBlockOption.None);
                return new
                {
                    GeneratedCount = generated.Count,
                    Items = generated.Take(200).Select(o => new
                    {
                        Name = Convert.ToString(o.GetAttribute("Name")), Type = o.GetType().Name
                    }).ToArray(),
                    ItemsTruncated = generated.Count > 200,
                    Saved = false, Downloaded = false,
                    Message = "Source generation can replace blocks. Compile and inspect before saving. A generation error does not guarantee restoration of previously replaced blocks."
                };
            }
            finally
            {
                try { source.Delete(); }
                catch (EngineeringException ex)
                {
                    _logger?.LogWarning(ex, "Could not remove temporary external source {SourceName} after generation.", sourceName);
                }
            }
        }

        internal static void ValidateSourceFile(string path, bool acceptBlockReplacement)
        {
            if (!acceptBlockReplacement)
                throw new ArgumentException("Source generation may replace existing blocks. Set acceptBlockReplacement=true only when this is intended.");
            var extension = Path.GetExtension(path ?? "");
            if (!new[] { ".scl", ".awl", ".db" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException("Use a Siemens .scl, .awl or .db source file.");
            var file = new FileInfo(path!);
            if (!file.Exists || file.Length == 0 || file.Length > 2 * 1024 * 1024)
                throw new ArgumentException("Source file must exist and contain between 1 byte and 2 MiB.");
        }

        public object[] GetDeviceModuleSummary(Device device)
        {
            return DeviceItems(device.DeviceItems).Take(201).Select(item => (object)new
            {
                item.Name, item.TypeIdentifier,
                OrderNumber = ReadOptionalAttribute(item, "OrderNumber"),
                FirmwareVersion = ReadOptionalAttribute(item, "FirmwareVersion"),
                TypeName = ReadOptionalAttribute(item, "TypeName"),
                Classification = ReadOptionalAttribute(item, "Classification")
            }).ToArray();
        }

        private static IEnumerable<DeviceItem> DeviceItems(DeviceItemComposition items)
        {
            foreach (var item in items)
            {
                yield return item;
                foreach (var child in DeviceItems(item.DeviceItems)) yield return child;
            }
        }

        private static string? ReadOptionalAttribute(IEngineeringObject item, string name)
        {
            if (!item.GetAttributeInfos().Any(a => a.Name == name)) return null;
            return Convert.ToString(item.GetAttribute(name));
        }
    }
}
