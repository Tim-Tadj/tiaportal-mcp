using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.SW.WatchAndForceTables;
using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using TiaMcpServer.Security;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        private const int MaxWatchTableBytes = 8 * 1024 * 1024;
        private const string WatchTableElement = "SW.WatchAndForceTables.PlcWatchTable";

        private PlcWatchAndForceTableGroup WatchTableGroup(string softwarePath, string[] groupPath)
        {
            PlcWatchAndForceTableGroup group = RequirePlcSoftware(softwarePath).WatchAndForceTableGroup;
            foreach (var name in groupPath)
            {
                group = group.Groups.SingleOrDefault(item => item.Name == name)
                    ?? throw new ArgumentException("No watch-table group has that exact name.");
            }
            return group;
        }

        public object ListWatchTables(string softwarePath, string[] groupPath, int limit, string cursor)
        {
            var group = WatchTableGroup(softwarePath, groupPath);
            var rows = group.Groups.Select(item => new { kind = "group", name = item.Name, consistent = (bool?)null, table = (PlcWatchTable?)null })
                .Concat(group.WatchTables.Select(item => new { kind = "watchTable", name = item.Name, consistent = (bool?)item.IsConsistent, table = (PlcWatchTable?)item }))
                .OrderBy(item => item.kind, StringComparer.Ordinal).ThenBy(item => item.name, StringComparer.Ordinal).ToArray();
            var pageRows = ModelContextProtocol.McpServer.PageItems(rows, limit, cursor,
                "watch-tables:" + ApiProjectScope() + "\n" + softwarePath + "\n" + string.Join("\n", groupPath), out var page);
            var items = pageRows.Select(row => new
            {
                row.kind,
                row.name,
                row.consistent,
                reference = row.table == null ? null : ApiProject(row.table, "", _ => WatchTableGroup(softwarePath, groupPath).WatchTables.Find(row.name))
            }).ToArray();
            return new { items, page };
        }

        public object ExportWatchTable(string expectedProjectPath, string softwarePath, string[] groupPath, string tableName, string exportPath)
        {
            RequireSameSelectedProject(expectedProjectPath);
            var table = WatchTableGroup(softwarePath, groupPath).WatchTables.Find(tableName);
            if (table == null || table.Name != tableName) throw new ArgumentException("No watch table has that exact name.");
            return ExportWatchTableFile(table, exportPath, ExportOptions.WithDefaults);
        }

        internal static object ExportWatchTableFile(PlcWatchTable table, string exportPath, ExportOptions options)
        {
            ProfileAccessPolicy.DemandWrite("export watch table");
            if (!Enum.IsDefined(typeof(ExportOptions), options)) throw new ArgumentException("Unsupported watch-table export option.");
            var path = OutputPathPolicy.ResolveDirectory(exportPath);
            if (!string.Equals(Path.GetExtension(path), ".xml", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose a new .xml file under the configured output root.");
            if (File.Exists(path) || Directory.Exists(path))
                throw new ArgumentException("The watch-table export path already exists. Choose a new file.");
            if (!Directory.Exists(Path.GetDirectoryName(path)))
                throw new ArgumentException("The watch-table export directory does not exist.");
            table.Export(new FileInfo(path), options);
            return new { name = table.Name, consistent = table.IsConsistent, path };
        }

        internal static string ReadWatchTableName(Stream source)
        {
            using var reader = XmlReader.Create(source, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxWatchTableBytes
            });
            var root = XDocument.Load(reader).Root;
            var tables = root?.Elements(WatchTableElement).ToArray();
            if (root?.Name != "Document" || tables?.Length != 1 ||
                root.Elements().Any(element => element.Name != WatchTableElement && element.Name != "Engineering" && element.Name != "DocumentInfo"))
                throw new ArgumentException("Supply a SimaticML document containing exactly one watch table.");
            var name = (string?)tables[0].Element("AttributeList")?.Element("Name");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("The watch table in the document has no name.");
            return name!;
        }

        public object ImportWatchTable(string expectedProjectPath, string softwarePath, string[] groupPath, string tableName, string importPath, bool replaceExisting)
        {
            ProfileAccessPolicy.DemandWrite("import watch table");
            RequireSameSelectedProject(expectedProjectPath);
            var path = OutputPathPolicy.ResolveApiPath(importPath);
            if (!string.Equals(Path.GetExtension(path), ".xml", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Supply a SimaticML .xml watch-table file.");
            byte[] xml;
            using (var input = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Length > MaxWatchTableBytes) throw new ArgumentException("The watch-table XML exceeds 8 MiB.");
                xml = new byte[(int)input.Length];
                for (var offset = 0; offset < xml.Length;)
                {
                    var count = input.Read(xml, offset, xml.Length - offset);
                    if (count == 0) throw new IOException("The watch-table XML could not be read in full.");
                    offset += count;
                }
            }
            using var document = new MemoryStream(xml);
            if (ReadWatchTableName(document) != tableName) throw new ArgumentException("The XML watch-table name does not match the requested name.");
            var group = WatchTableGroup(softwarePath, groupPath);
            var existing = group.WatchTables.Find(tableName);
            if (replaceExisting ? existing?.Name != tableName : existing != null)
                throw new ArgumentException("The existing watch table differs from the requested create or replace mode.");

            var stagingDirectory = OutputPathPolicy.ResolveDirectory(Path.Combine(".mcp-content", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(stagingDirectory);
            var stagingPath = OutputPathPolicy.ResolveDirectory(Path.Combine(stagingDirectory, "watch-table.xml"));
            try
            {
                File.WriteAllBytes(stagingPath, xml);
                RequireSameSelectedProject(expectedProjectPath);
                try
                {
                    group.WatchTables.Import(new FileInfo(stagingPath), replaceExisting ? ImportOptions.Override : ImportOptions.None);
                    var imported = group.WatchTables.Find(tableName);
                    if (imported?.Name != tableName) throw new InvalidOperationException("The imported table could not be read back.");
                    return new { name = imported.Name, consistent = imported.IsConsistent, replaced = replaceExisting, saved = false };
                }
                catch (EngineeringException exception)
                {
                    throw new InvalidOperationException("Watch-table import may have changed the project. List or export the table before retrying. " + exception.Message, exception);
                }
            }
            finally
            {
                try
                {
                    File.Delete(stagingPath);
                    Directory.Delete(stagingDirectory);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    _logger?.LogWarning(exception, "Could not clean up staged watch-table XML {StagingPath}.", stagingPath);
                }
            }
        }
    }
}
