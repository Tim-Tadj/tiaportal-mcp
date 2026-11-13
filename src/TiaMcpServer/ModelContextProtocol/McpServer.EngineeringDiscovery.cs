using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using TiaMcp.Contracts;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public sealed partial class McpServer
    {
        [McpServerTool(Name = "DescribeEngineeringObject", ReadOnly = true), Description("Describe one exact discovered device, deviceItem, plcSoftware, block or type. Supply softwarePath for block and type. Returns bounded typed metadata; no arbitrary object access.")]
        public static object DescribeEngineeringObject(string kind, string path, string softwarePath = "")
        {
            return ProjectCall(() => Portal.DescribeEngineeringDiscovery(kind, path, softwarePath));
        }

        [McpServerTool(Name = "GetEngineeringAttributes", ReadOnly = true), Description("Read a page of public attributes for an exact device, deviceItem, plcSoftware, block or type. Supply softwarePath for block and type. Attribute values follow the existing bounded Full-detail policy.")]
        public static PagedResponse<Attribute> GetEngineeringAttributes(string kind, string path, string softwarePath = "", int limit = PageRequest.DefaultLimit, string cursor = "")
        {
            return ProjectCall(() =>
            {
                return Portal.GetEngineeringDiscoveryAttributes(kind, path, softwarePath, limit, cursor);
            });
        }

        [McpServerTool(Name = "ListEngineeringChildren", ReadOnly = true), Description("List a paged approved collection: devices in the selected project, deviceItems at an exact parent item path, or tagTables, externalSources, blocks or types in an exact PLC software path. Supply the parent path in softwarePath. Use the returned canonical paths for further inspection.")]
        public static CallToolResult ListEngineeringChildren(string collection, string softwarePath = "", int limit = PageRequest.DefaultLimit, string cursor = "", ResponseFormat responseFormat = ResponseFormat.Auto)
        {
            return ProjectCall(() =>
            {
                switch (collection)
                {
                    case "deviceItems":
                        McpResponsePresenter.ValidateRequest(responseFormat, ResponseDetailLevel.Summary);
                        var children = Portal.GetEngineeringDeviceItemChildren(softwarePath, limit, cursor);
                        return McpResponsePresenter.Present(children, children.Items, children.Page, responseFormat, ResponseDetailLevel.Summary, "items", new[]
                        {
                            new TabularColumn<EngineeringChild>("kind", x => TabularCell.From(x.Kind)),
                            new TabularColumn<EngineeringChild>("name", x => TabularCell.From(x.Name)),
                            new TabularColumn<EngineeringChild>("path", x => TabularCell.From(x.Path))
                        });
                    case "devices": return McpListTools.GetDevices(ResponseDetailLevel.Summary, limit, cursor, responseFormat);
                    case "tagTables": return GetPlcTagTables(softwarePath, limit, cursor, responseFormat);
                    case "externalSources": return GetExternalSources(softwarePath, limit, cursor, responseFormat);
                    case "blocks": return McpListTools.GetBlocks(softwarePath, "", ResponseDetailLevel.Summary, limit, cursor, responseFormat);
                    case "types": return McpListTools.GetTypes(softwarePath, "", ResponseDetailLevel.Summary, limit, cursor, responseFormat);
                    default: throw new ArgumentException("Supported collections are devices, deviceItems, tagTables, externalSources, blocks and types.");
                }
            });
        }
    }
}
