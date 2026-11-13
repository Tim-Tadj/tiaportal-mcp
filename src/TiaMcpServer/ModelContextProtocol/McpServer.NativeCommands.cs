using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace TiaMcpServer.ModelContextProtocol
{
    public sealed partial class McpServer
    {
        [McpServerTool(Name = "DescribeEngineeringCreation", ReadOnly = true), Description("Describe the exact native composition creation types and named parameters for an engineering object or composition reference. Parameter types are reported when inferable from public members; otherwise use an explicit typed value.")]
        public static object DescribeEngineeringCreation(string entityRef = "", string composition = "", int limit = PageRequest.DefaultLimit, string cursor = "")
        {
            return ProjectCall(() => Portal.DescribeEngineeringCreation(entityRef, composition, limit, cursor));
        }

        [McpServerTool(Name = "DescribeEngineeringActions", ReadOnly = true), Description("Describe exact native action names and ordered parameter schemas for an engineering object or composition reference. Action effects are unclassified.")]
        public static object DescribeEngineeringActions(string entityRef = "", int limit = PageRequest.DefaultLimit, string cursor = "")
        {
            return ProjectCall(() => Portal.DescribeEngineeringActions(entityRef, limit, cursor));
        }

#if TIA_MCP_READ_WRITE
        [McpServerTool(Name = "CreateEngineeringObject", ReadOnly = false, Destructive = true), Description("Create an object through a declared native composition using the exact type and named parameters from DescribeEngineeringCreation. Requires exact expectedProjectPath and acceptNativeEffects=true. Native effects are unclassified and may partially apply on failure.")]
        public static object? CreateEngineeringObject(string typeName, JsonElement parameters, string expectedProjectPath, bool acceptNativeEffects, string entityRef = "", string composition = "")
        {
            return ProjectCall(() => Portal.CreateEngineeringObject(entityRef, composition, typeName, parameters, expectedProjectPath, acceptNativeEffects));
        }

        [McpServerTool(Name = "UpdateEngineeringAttributes", ReadOnly = false, Destructive = true), Description("Update declared writable native attributes with exact expectedValue and value fields. For write-only attributes expectedValue must be null. Requires exact expectedProjectPath and acceptNativeEffects=true. Effects are unclassified; multi-field updates may partially apply on failure.")]
        public static object UpdateEngineeringAttributes(JsonElement changes, string expectedProjectPath, bool acceptNativeEffects, string entityRef = "")
        {
            return ProjectCall(() => Portal.UpdateEngineeringAttributes(entityRef, changes, expectedProjectPath, acceptNativeEffects));
        }

        [McpServerTool(Name = "InvokeEngineeringAction", ReadOnly = false, Destructive = true), Description("Invoke one exact native action with ordered arguments from DescribeEngineeringActions. Effects are unclassified and may modify the project or other native state. Requires the exact expectedProjectPath and acceptNativeEffects=true.")]
        public static object? InvokeEngineeringAction(string actionName, JsonElement arguments, string expectedProjectPath, bool acceptNativeEffects, string entityRef = "")
        {
            return ProjectCall(() => Portal.InvokeEngineeringAction(entityRef, actionName, arguments, expectedProjectPath, acceptNativeEffects));
        }
#endif
    }
}
