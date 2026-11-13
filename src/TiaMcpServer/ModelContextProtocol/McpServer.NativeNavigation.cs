using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using TiaMcp.Contracts;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public sealed partial class McpServer
    {
        [McpServerTool(Name = "DescribeEngineeringSurface", ReadOnly = true), Description("Describe public composition, attribute, action and service metadata for a native or API engineering reference. Empty reference selects the project. Use GetApiService or DescribeEngineeringActions for the corresponding schemas.")]
        public static object DescribeEngineeringSurface(string entityRef = "")
        {
            return ProjectCall(() => Portal.DescribeNativeSurface(entityRef));
        }

        [McpServerTool(Name = "ReadEngineeringSurfaceAttributes", ReadOnly = true), Description("Read one bounded page of public attribute values on a native entity reference. Sensitive names are redacted and individual read failures are reported.")]
        public static PagedResponse<Attribute> ReadEngineeringSurfaceAttributes(string entityRef = "", int limit = PageRequest.DefaultLimit, string cursor = "")
        {
            return ProjectCall(() => Portal.ReadNativeAttributes(entityRef, limit, cursor));
        }

        [McpServerTool(Name = "BrowseEngineeringObject", ReadOnly = true), Description("Browse a page of a declared native composition. Empty entityRef selects the project; use returned references for children. $Software bridges a device item to its software. References are session-issued and may expire; browse the parent again if stale.")]
        public static CallToolResult BrowseEngineeringObject(string composition, string entityRef = "", int limit = PageRequest.DefaultLimit, string cursor = "", ResponseFormat responseFormat = ResponseFormat.Auto)
        {
            return ProjectCall(() =>
            {
                McpResponsePresenter.ValidateRequest(responseFormat, ResponseDetailLevel.Summary);
                var result = Portal.BrowseNativeComposition(entityRef, composition, limit, cursor);
                return McpResponsePresenter.Present(result, result.Items, result.Page, responseFormat, ResponseDetailLevel.Summary, "items", new[]
                {
                    new TabularColumn<NativeChild>("name", x => TabularCell.From(x.Name)),
                    new TabularColumn<NativeChild>("kind", x => TabularCell.From(x.Kind)),
                    new TabularColumn<NativeChild>("entityRef", x => TabularCell.From(x.EntityRef))
                });
            });
        }
    }
}
