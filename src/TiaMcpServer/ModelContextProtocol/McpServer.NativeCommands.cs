using ModelContextProtocol.Server;
using System.ComponentModel;

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
    }
}
