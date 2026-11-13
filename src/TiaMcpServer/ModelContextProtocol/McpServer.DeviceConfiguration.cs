using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    public sealed partial class McpServer
    {
#if TIA_MCP_READ_WRITE
        [McpServerTool(Name = "ExportDeviceConfiguration", ReadOnly = false, Destructive = false), Description("Export one exact offline device through Siemens CAx into a new unique directory beneath the allowed output root. Local projects only. Returns AML and native log paths and native success status; partial files can remain on failure. Does not save, download or inspect live device health.")]
        public static object ExportDeviceConfiguration(string expectedProjectPath, string devicePath, string outputDirectory = "")
        {
            return ProjectCall(() => Portal.ExportDeviceConfiguration(expectedProjectPath, devicePath, outputDirectory));
        }
#endif
    }
}
