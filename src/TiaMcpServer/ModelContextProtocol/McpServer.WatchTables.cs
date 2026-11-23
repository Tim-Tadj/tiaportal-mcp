using ModelContextProtocol.Server;
using System;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    public sealed partial class McpServer
    {
        [McpServerTool(Name = "ListWatchTables", ReadOnly = true), Description("List the direct subgroups and offline watch tables under an exact PLC software and group path. Pass group names in order, or omit the group path for the root.")]
        public static object ListWatchTables(string softwarePath, string[]? groupPath = null, int limit = PageRequest.DefaultLimit, string cursor = "")
        {
            return ProjectCall(() => Portal.ListWatchTables(softwarePath, groupPath ?? Array.Empty<string>(), limit, cursor));
        }

#if TIA_MCP_READ_WRITE
        [McpServerTool(Name = "ExportWatchTable", ReadOnly = false, Destructive = false), Description("Export one exact offline watch table as SimaticML to a new XML file below the output root. Requires the exact project path.")]
        public static object ExportWatchTable(string expectedProjectPath, string softwarePath, string tableName, string exportPath, string[]? groupPath = null)
        {
            return ProjectCall(() => Portal.ExportWatchTable(expectedProjectPath, softwarePath, groupPath ?? Array.Empty<string>(), tableName, exportPath));
        }

        [McpServerTool(Name = "ImportWatchTable", ReadOnly = false, Destructive = true), Description("Import exactly one offline watch table from SimaticML after checking project, group, table name and file root. Use replaceExisting only for a table that already exists. Read back and save explicitly.")]
        public static object ImportWatchTable(string expectedProjectPath, string softwarePath, string tableName, string importPath, bool replaceExisting = false, string[]? groupPath = null)
        {
            return ProjectCall(() => Portal.ImportWatchTable(expectedProjectPath, softwarePath, groupPath ?? Array.Empty<string>(), tableName, importPath, replaceExisting));
        }
#endif
    }
}
