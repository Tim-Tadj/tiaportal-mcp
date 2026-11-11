using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetAccessibleDevices", ReadOnly = true), Description("Discover devices through Siemens on one explicitly selected PC interface. Read-only; no name/IP assignment, download or CPU state change.")]
        public static object GetAccessibleDevices(string deviceItemPath, string mode, string pcInterfaceName, int pcInterfaceNumber)
        {
            return OnlineCall(() => Portal.GetAccessibleDevices(deviceItemPath, mode, pcInterfaceName, pcInterfaceNumber));
        }

        [McpServerTool(Name = "GetHardwareNetworkInfo", ReadOnly = true), Description("Read offline hardware attributes, I/O addresses, subnet and controller assignment at an exact device-item path.")]
        public static object GetHardwareNetworkInfo(string deviceItemPath)
        {
            return OnlineCall(() => Portal.GetHardwareNetworkInfo(deviceItemPath));
        }

        [McpServerTool(Name = "ChangeHardwareType", ReadOnly = false, Destructive = true), Description("Replace an offline configured module using Siemens ChangeType after checking its expected current type. Does not save or download. Read back module addresses and controller assignment, then compile before saving.")]
        public static object ChangeHardwareType(string deviceItemPath, string expectedTypeIdentifier, string newTypeIdentifier)
        {
            return OnlineCall(() => Portal.ChangeHardwareType(deviceItemPath, expectedTypeIdentifier, newTypeIdentifier));
        }

        [McpServerTool(Name = "CompileHardware", ReadOnly = false, Destructive = false), Description("Compile hardware at an exact device-item path and return all compiler messages. Does not save or download.")]
        public static object CompileHardware(string deviceItemPath)
        {
            return OnlineCall(() => Portal.CompileHardware(deviceItemPath));
        }

    }
}
