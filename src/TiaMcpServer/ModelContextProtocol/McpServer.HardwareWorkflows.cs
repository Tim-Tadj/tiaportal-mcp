using ModelContextProtocol.Server;
using System.ComponentModel;
using ModelContextProtocol.Protocol;
using TiaMcp.Contracts;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public sealed partial class McpServer
    {
        [McpServerTool(Name = "GetTiaProcesses", ReadOnly = true), Description("List up to 200 running TIA Portal processes and their reported project paths without attaching.")]
        public static CallToolResult GetTiaProcesses(int limit = PageRequest.DefaultLimit, string cursor = "", ResponseFormat responseFormat = ResponseFormat.Auto)
        {
            return ProjectCall(() =>
            {
                McpResponsePresenter.ValidateRequest(responseFormat, ResponseDetailLevel.Summary);
                var rows = PageItems(Portal.GetTiaProcesses(), limit, cursor, "tia-processes", out var page);
                return McpResponsePresenter.Present(new PagedResponse<TiaProcessEntry> { Items = rows, Page = page }, rows, page,
                    responseFormat, ResponseDetailLevel.Summary, "items", new[]
                    {
                        new TabularColumn<TiaProcessEntry>("id", x => TabularCell.From(x.Id)),
                        new TabularColumn<TiaProcessEntry>("mode", x => TabularCell.From(x.Mode)),
                        new TabularColumn<TiaProcessEntry>("path", x => TabularCell.From(x.Path)),
                        new TabularColumn<TiaProcessEntry>("projectPath", x => TabularCell.From(x.ProjectPath))
                    });
            });
        }

        [McpServerTool(Name = "FindHardwareCatalog", ReadOnly = true), Description("Search the V19 or later hardware catalogue by article number or text. Results are paged; searches exceeding 1000 matches must be narrowed.")]
        public static CallToolResult FindHardwareCatalog(string search, int limit = PageRequest.DefaultLimit, string cursor = "", ResponseFormat responseFormat = ResponseFormat.Auto)
        {
            return ProjectCall(() =>
            {
                McpResponsePresenter.ValidateRequest(responseFormat, ResponseDetailLevel.Summary);
                var rows = PageItems(Portal.FindHardwareCatalog(search), limit, cursor, "hardware-catalog:" + search, out var page);
                return McpResponsePresenter.Present(new PagedResponse<HardwareCatalogEntry> { Items = rows, Page = page }, rows, page,
                    responseFormat, ResponseDetailLevel.Summary, "items", new[]
                    {
                        new TabularColumn<HardwareCatalogEntry>("articleNumber", x => TabularCell.From(x.ArticleNumber)),
                        new TabularColumn<HardwareCatalogEntry>("typeIdentifier", x => TabularCell.From(x.TypeIdentifier)),
                        new TabularColumn<HardwareCatalogEntry>("version", x => TabularCell.From(x.Version)),
                        new TabularColumn<HardwareCatalogEntry>("typeName", x => TabularCell.From(x.TypeName))
                    });
            });
        }

#if TIA_MCP_READ_WRITE
        [McpServerTool(Name = "CreateProfinetIoSystem", ReadOnly = false, Destructive = false), Description("Create a named offline I/O system on an exact controller after checking project path, subnet and vacancy. Does not save or download. On failure, read back before retrying.")]
        public static object CreateProfinetIoSystem(string controllerPath, int controllerIndex, int controllerNodeIndex, string expectedSubnet, string systemName, int systemNumber, string expectedProjectPath)
        {
            return ProjectCall(() => Portal.CreateProfinetIoSystem(controllerPath, controllerIndex, controllerNodeIndex, expectedSubnet, systemName, systemNumber, expectedProjectPath));
        }

        [McpServerTool(Name = "CreateHardwareDevice", ReadOnly = false, Destructive = false), Description("Create one offline device in an exact group after verifying the project path and exact catalogue type. Empty group path selects the project root; UngroupedDevices selects that group. Does not overwrite, save or download.")]
        public static object CreateHardwareDevice(string expectedProjectPath, string groupPath, string catalogueSearch, string typeIdentifier, string deviceName, string itemName)
        {
            return ProjectCall(() => Portal.CreateHardwareDevice(expectedProjectPath, groupPath, catalogueSearch, typeIdentifier, deviceName, itemName));
        }

        [McpServerTool(Name = "PlugHardwareModule", ReadOnly = false, Destructive = false), Description("Plug one exact catalogue module into an explicit vacant slot after checking the parent type and Siemens CanPlugNew. Offline only; does not save or download.")]
        public static object PlugHardwareModule(string expectedProjectPath, string parentPath, string expectedParentType, int slot, string typeIdentifier, string name)
        {
            return ProjectCall(() => Portal.PlugHardwareModule(expectedProjectPath, parentPath, expectedParentType, slot, typeIdentifier, name));
        }

        [McpServerTool(Name = "ConfigureIoAddress", ReadOnly = false, Destructive = false), Description("Set the native start address of one indexed offline I/O address, checking its direction and expected current start address. Checks overlaps when shared address controllers are identifiable; Siemens compile must validate all ranges and process images. Does not save or download.")]
        public static object ConfigureIoAddress(string expectedProjectPath, string deviceItemPath, int addressIndex, string expectedDirection, int expectedStartAddress, int startAddress)
        {
            return ProjectCall(() => Portal.ConfigureIoAddress(expectedProjectPath, deviceItemPath, addressIndex, expectedDirection, expectedStartAddress, startAddress));
        }

        [McpServerTool(Name = "ConfigureHardwareNode", ReadOnly = false, Destructive = false), Description("Set an indexed offline node's IPv4 address, mask and PROFINET name after checking all expected current values. Does not save or download. A failure can leave earlier changes applied; read back before retrying.")]
        public static object ConfigureHardwareNode(string expectedProjectPath, string deviceItemPath, int nodeIndex, string expectedAddress, string expectedSubnetMask, string expectedPnDeviceName, string address, string subnetMask, string pnDeviceName)
        {
            return ProjectCall(() => Portal.ConfigureHardwareNode(expectedProjectPath, deviceItemPath, nodeIndex, expectedAddress, expectedSubnetMask, expectedPnDeviceName, address, subnetMask, pnDeviceName));
        }
        [McpServerTool(Name = "AssignProfinetDevice", ReadOnly = false, Destructive = true), Description("Connect an indexed offline device node and connector to an exact controller subnet and I/O system after checking existing and target bindings. Replacing existing bindings requires replaceExisting=true. Does not save or download. Failure can leave earlier changes applied; read back before retrying.")]
        public static object AssignProfinetDevice(string expectedProjectPath, string devicePath, int nodeIndex, int connectorIndex, string controllerPath, int controllerNodeIndex, int controllerIndex, string expectedSubnet, string expectedSystem, int expectedSystemNumber, string expectedCurrentSubnet, string expectedCurrentSystem, int? expectedCurrentSystemNumber, string expectedCurrentControllerPath, int expectedCurrentControllerIndex, bool replaceExisting)
        {
            return ProjectCall(() => Portal.AssignProfinetDevice(expectedProjectPath, devicePath, nodeIndex, connectorIndex, controllerPath, controllerNodeIndex, controllerIndex, expectedSubnet, expectedSystem, expectedSystemNumber, expectedCurrentSubnet, expectedCurrentSystem, expectedCurrentSystemNumber, expectedCurrentControllerPath, expectedCurrentControllerIndex, replaceExisting));
        }
#endif
    }
}
