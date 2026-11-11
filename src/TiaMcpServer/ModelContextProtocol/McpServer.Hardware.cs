using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    public sealed partial class McpServer
    {
        [McpServerTool(Name = "GetAccessibleDevices", ReadOnly = true), Description("V19 or later only. Discover devices through Siemens on one explicitly selected PC interface. Read-only; no name/IP assignment, download or CPU state change.")]
        public static object GetAccessibleDevices(string deviceItemPath, string mode, string pcInterfaceName, int pcInterfaceNumber)
        {
            return OnlineCall(() => Portal.GetAccessibleDevices(deviceItemPath, mode, pcInterfaceName, pcInterfaceNumber));
        }

        [McpServerTool(Name = "GetHardwareNetworkInfo", ReadOnly = true), Description("Read offline hardware attributes, I/O addresses, subnet and controller assignment at an exact device-item path.")]
        public static object GetHardwareNetworkInfo(string deviceItemPath)
        {
            return OnlineCall(() => Portal.GetHardwareNetworkInfo(deviceItemPath));
        }

#if TIA_MCP_READ_WRITE
        [McpServerTool(Name = "ChangeHardwareType", ReadOnly = false, Destructive = true), Description("V18 or later only. Replace an offline configured module using Siemens ChangeType after checking its expected current type. Does not save or download. Read back module addresses and controller assignment, then compile before saving.")]
        public static object ChangeHardwareType(string deviceItemPath, string expectedTypeIdentifier, string newTypeIdentifier)
        {
            return OnlineCall(() => Portal.ChangeHardwareType(deviceItemPath, expectedTypeIdentifier, newTypeIdentifier));
        }

        [McpServerTool(Name = "CompileHardware", ReadOnly = false, Destructive = false), Description("Compile hardware at an exact device-item path and return all compiler messages. A classic SIMATIC PC rack compiles its parent PC station. Does not save or download.")]
        public static object CompileHardware(string deviceItemPath)
        {
            return OnlineCall(() => Portal.CompileHardware(deviceItemPath));
        }
        [McpServerTool(Name = "SetBaseUnitPotentialGroup", ReadOnly = false, Destructive = false), Description("Set an offline ET 200SP module to a light BaseUnit (new potential group) or dark BaseUnit (continue from left), checking the expected current setting: 0 dark, 1 light. Match the fitted BaseUnit, then compile. Does not save, download or change wiring.")]
        public static object SetBaseUnitPotentialGroup(string deviceItemPath, int expectedValue, bool lightBaseUnit)
        {
            return OnlineCall(() => Portal.SetBaseUnitPotentialGroup(deviceItemPath, expectedValue, lightBaseUnit));
        }
#endif


    }
}
