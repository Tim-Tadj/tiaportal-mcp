using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace TiaMcpServer.ModelContextProtocol
{
    public sealed partial class McpServer
    {
        [McpServerTool(Name = "ListApiTypes", ReadOnly = true), Description("List a page of public Siemens Engineering types from loaded or exactly referenced trusted assemblies. Missing and partially loadable assemblies are reported. Requires an attached Portal; no selected project is needed.")]
        public static object ListApiTypes(string filter = "", string namespacePrefix = "", int limit = PageRequest.DefaultLimit, string cursor = "")
        {
            return ProjectCall(() => Portal.ListApiTypes(filter, namespacePrefix, limit, cursor));
        }

        [McpServerTool(Name = "DescribeApiObject", ReadOnly = true), Description("Describe a page of public Siemens instance properties and exact method schemas on an issued reference. Empty entityRef selects the project; $portal selects the attached Portal even without a project. Method effects are unclassified; schema availability does not mean an operation is safe.")]
        public static object DescribeApiObject(string entityRef = "", string filter = "", int limit = PageRequest.DefaultLimit, string cursor = "")
        {
            return ProjectCall(() => Portal.DescribeApiObject(entityRef, filter, limit, cursor));
        }

        [McpServerTool(Name = "ReadApiProperty", ReadOnly = true), Description("Read a public property or indexed property by its exact schema id. Complex results are issued as bounded session references; sensitive property values are redacted.")]
        public static object? ReadApiProperty(string propertyId, string entityRef = "", JsonElement? indices = null)
        {
            return ProjectCall(() => Portal.ReadApiProperty(entityRef, propertyId, indices.GetValueOrDefault()));
        }

        [McpServerTool(Name = "ReadApiField", ReadOnly = true), Description("Read a public field by exact schema id; sensitive fields are redacted.")]
        public static object? ReadApiField(string fieldId, string entityRef = "")
        {
            return ProjectCall(() => Portal.ReadApiField(entityRef, fieldId));
        }

        [McpServerTool(Name = "BrowseApiCollection", ReadOnly = true), Description("Browse an issued Siemens collection reference in pages of at most 200 items, with a 5000-item navigation limit. Changes between pages may change results.")]
        public static object BrowseApiCollection(string entityRef, int limit = PageRequest.DefaultLimit, string cursor = "")
        {
            return ProjectCall(() => Portal.BrowseApiCollection(entityRef, limit, cursor));
        }

        [McpServerTool(Name = "GetApiService", ReadOnly = true), Description("Get one exact declared public Siemens service for an issued engineering object. The service type must appear in DescribeApiObject.")]
        public static object? GetApiService(string serviceType, string entityRef = "")
        {
            return ProjectCall(() => Portal.GetApiService(entityRef, serviceType));
        }

#if TIA_MCP_READ_WRITE
        [McpServerTool(Name = "InvokeApiMethod", ReadOnly = false, Destructive = false), Description("Invoke an exact Siemens method marked callable by DescribeApiObject. Requires the owning project path and explicit acceptance of its declared effects. Arguments are a JSON array in schema order.")]
        public static object? InvokeApiMethod(string methodId, JsonElement arguments, string expectedProjectPath, bool acceptNativeEffects, string entityRef = "", string[]? genericTypes = null)
        {
            return ProjectCall(() => Portal.InvokeApiMethod(entityRef, methodId, arguments, expectedProjectPath, acceptNativeEffects, genericTypes ?? System.Array.Empty<string>()));
        }
#endif
    }
}
