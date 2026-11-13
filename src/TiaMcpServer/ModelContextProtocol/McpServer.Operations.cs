using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using TiaMcp.Contracts;
using TiaMcpServer.Runtime;

namespace TiaMcpServer.ModelContextProtocol
{
    public sealed partial class McpServer
    {
        [McpServerTool(Name = "GetOperations", ReadOnly = true), Description("List advertised operations with domain, effects, profile and runtime availability. Filter by domain or name and follow the cursor.")]
        public static CallToolResult GetOperations(string domain = "", string name = "", int limit = PageRequest.DefaultLimit, string cursor = "", ResponseFormat responseFormat = ResponseFormat.Auto)
        {
            return ProjectCall(() =>
            {
                if (domain == null || name == null || domain.Length > 128 || name.Length > 128)
                    throw new ArgumentException("Domain and name filters must be at most 128 characters.");
                McpResponsePresenter.ValidateRequest(responseFormat, ResponseDetailLevel.Summary);
                var all = OperationRegistry.GetAll(WorkerBuild.Current.TiaMajorVersion)
                    .Where(x => x.Domain.IndexOf(domain, StringComparison.OrdinalIgnoreCase) >= 0 && x.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(x => new OperationSummary { Name = x.Name, Domain = x.Domain, Effect = x.Effect,
                        Profile = x.Profile, Status = x.Status, RuntimeAvailability = x.RuntimeAvailability }).ToList();
                var rows = PageItems(all, limit, cursor, "operations:" + domain + "\n" + name, out var page);
                return McpResponsePresenter.Present(new PagedResponse<OperationSummary> { Items = rows, Page = page }, rows, page,
                    responseFormat, ResponseDetailLevel.Summary, "items", new[]
                    {
                        new TabularColumn<OperationSummary>("name", x => TabularCell.From(x.Name)),
                        new TabularColumn<OperationSummary>("domain", x => TabularCell.From(x.Domain)),
                        new TabularColumn<OperationSummary>("effect", x => TabularCell.From(x.Effect)),
                        new TabularColumn<OperationSummary>("profile", x => TabularCell.From(x.Profile)),
                        new TabularColumn<OperationSummary>("status", x => TabularCell.From(x.Status)),
                        new TabularColumn<OperationSummary>("runtimeAvailability", x => TabularCell.From(x.RuntimeAvailability))
                    });
            });
        }

        [McpServerTool(Name = "GetOperationInfo", ReadOnly = true), Description("Get metadata for one exact advertised operation name. Use the MCP tool schema for parameter types and full descriptions.")]
        public static OperationDetails GetOperationInfo(string name)
        {
            return ProjectCall(() =>
            {
                if (string.IsNullOrWhiteSpace(name) || name.Length > 128)
                    throw new ArgumentException("Supply an exact operation name of 1 to 128 characters.");
                return OperationRegistry.GetDetails(name, WorkerBuild.Current.TiaMajorVersion);
            });
        }
    }
}
