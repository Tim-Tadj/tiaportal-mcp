using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Collections.Generic;
using System.ComponentModel;
using TiaMcp.Contracts;

namespace TiaMcpServer.ModelContextProtocol
{
    [McpServerToolType]
    public static class McpListTools
    {
        private static readonly IReadOnlyList<TabularColumn<ResponseProjectInfo>>
            ProjectColumns = new[]
            {
                new TabularColumn<ResponseProjectInfo>(
                    "path",
                    item => TabularCell.From(item.Path)),
                new TabularColumn<ResponseProjectInfo>(
                    "name",
                    item => TabularCell.From(item.Name))
            };

        private static readonly IReadOnlyList<TabularColumn<ResponseDeviceInfo>>
            DeviceColumns = new[]
            {
                new TabularColumn<ResponseDeviceInfo>(
                    "path",
                    item => TabularCell.From(item.Path)),
                new TabularColumn<ResponseDeviceInfo>(
                    "name",
                    item => TabularCell.From(item.Name))
            };

        private static readonly IReadOnlyList<TabularColumn<ResponseBlockInfo>>
            BlockSummaryColumns = new[]
            {
                new TabularColumn<ResponseBlockInfo>(
                    "path",
                    item => TabularCell.From(item.Path)),
                new TabularColumn<ResponseBlockInfo>(
                    "name",
                    item => TabularCell.From(item.Name)),
                new TabularColumn<ResponseBlockInfo>(
                    "typeName",
                    item => TabularCell.From(item.TypeName)),
                new TabularColumn<ResponseBlockInfo>(
                    "programmingLanguage",
                    item => TabularCell.From(item.ProgrammingLanguage)),
                new TabularColumn<ResponseBlockInfo>(
                    "isConsistent",
                    item => TabularCell.From(item.IsConsistent))
            };

        private static readonly IReadOnlyList<TabularColumn<ResponseBlockInfo>>
            BlockStandardColumns = new[]
            {
                new TabularColumn<ResponseBlockInfo>(
                    "path",
                    item => TabularCell.From(item.Path)),
                new TabularColumn<ResponseBlockInfo>(
                    "name",
                    item => TabularCell.From(item.Name)),
                new TabularColumn<ResponseBlockInfo>(
                    "typeName",
                    item => TabularCell.From(item.TypeName)),
                new TabularColumn<ResponseBlockInfo>(
                    "namespace",
                    item => TabularCell.From(item.Namespace)),
                new TabularColumn<ResponseBlockInfo>(
                    "programmingLanguage",
                    item => TabularCell.From(item.ProgrammingLanguage)),
                new TabularColumn<ResponseBlockInfo>(
                    "memoryLayout",
                    item => TabularCell.From(item.MemoryLayout)),
                new TabularColumn<ResponseBlockInfo>(
                    "isConsistent",
                    item => TabularCell.From(item.IsConsistent)),
                new TabularColumn<ResponseBlockInfo>(
                    "headerName",
                    item => TabularCell.From(item.HeaderName)),
                new TabularColumn<ResponseBlockInfo>(
                    "modifiedDate",
                    item => TabularCell.From(item.ModifiedDate)),
                new TabularColumn<ResponseBlockInfo>(
                    "isKnowHowProtected",
                    item => TabularCell.From(item.IsKnowHowProtected))
            };

        private static readonly IReadOnlyList<TabularColumn<ResponseTypeInfo>>
            TypeSummaryColumns = new[]
            {
                new TabularColumn<ResponseTypeInfo>(
                    "path",
                    item => TabularCell.From(item.Path)),
                new TabularColumn<ResponseTypeInfo>(
                    "name",
                    item => TabularCell.From(item.Name)),
                new TabularColumn<ResponseTypeInfo>(
                    "typeName",
                    item => TabularCell.From(item.TypeName)),
                new TabularColumn<ResponseTypeInfo>(
                    "isConsistent",
                    item => TabularCell.From(item.IsConsistent))
            };

        private static readonly IReadOnlyList<TabularColumn<ResponseTypeInfo>>
            TypeStandardColumns = new[]
            {
                new TabularColumn<ResponseTypeInfo>(
                    "path",
                    item => TabularCell.From(item.Path)),
                new TabularColumn<ResponseTypeInfo>(
                    "name",
                    item => TabularCell.From(item.Name)),
                new TabularColumn<ResponseTypeInfo>(
                    "typeName",
                    item => TabularCell.From(item.TypeName)),
                new TabularColumn<ResponseTypeInfo>(
                    "namespace",
                    item => TabularCell.From(item.Namespace)),
                new TabularColumn<ResponseTypeInfo>(
                    "isConsistent",
                    item => TabularCell.From(item.IsConsistent)),
                new TabularColumn<ResponseTypeInfo>(
                    "modifiedDate",
                    item => TabularCell.From(item.ModifiedDate)),
                new TabularColumn<ResponseTypeInfo>(
                    "isKnowHowProtected",
                    item => TabularCell.From(item.IsKnowHowProtected))
            };

        [McpServerTool(Name = "ListProjects", ReadOnly = true),
         Description("List open projects and sessions with canonical paths. Auto uses CSV for Summary, TOON for Standard and compact JSON for Full.")]
        public static CallToolResult ListProjects(
            [Description("detailLevel: Summary returns canonical identity, Standard uses the same lossless fields, and Full also includes bounded raw attributes")] ResponseDetailLevel detailLevel = ResponseDetailLevel.Summary,
            [Description("responseFormat: Auto, Toon, Csv or Json. Keep Auto unless a downstream parser requires a specific format")] ResponseFormat responseFormat = ResponseFormat.Auto)
        {
            McpResponsePresenter.ValidateRequest(
                responseFormat,
                detailLevel);
            var response = McpServer.ListProjects(detailLevel);

            return McpResponsePresenter.Present(
                response,
                response.Items,
                null,
                responseFormat,
                detailLevel,
                "items",
                ProjectColumns);
        }

        [McpServerTool(Name = "GetDevices", ReadOnly = true),
         Description("List project devices using compact, deterministic and paged results. Auto uses CSV for Summary, TOON for Standard and compact JSON for Full.")]
        public static CallToolResult GetDevices(
            [Description("detailLevel: Summary and Standard return canonical identity, while Full includes raw attributes and diagnostic descriptions")] ResponseDetailLevel detailLevel = ResponseDetailLevel.Summary,
            [Description("limit: maximum devices to return. Defaults to 50 and is capped at 200")] int limit = PageRequest.DefaultLimit,
            [Description("cursor: opaque cursor returned by the previous page; omit for the first page")] string cursor = "",
            [Description("responseFormat: Auto, Toon, Csv or Json. Keep Auto unless a downstream parser requires a specific format")] ResponseFormat responseFormat = ResponseFormat.Auto)
        {
            McpResponsePresenter.ValidateRequest(
                responseFormat,
                detailLevel);
            var response = McpServer.GetDevices(detailLevel, limit, cursor);

            return McpResponsePresenter.Present(
                response,
                response.Items,
                response.Page,
                responseFormat,
                detailLevel,
                "items",
                DeviceColumns);
        }

        [McpServerTool(Name = "GetBlocks", ReadOnly = true),
         Description("List PLC blocks with canonical paths, compact detail and opaque paging. Auto uses CSV for Summary, TOON for Standard and compact JSON for Full.")]
        public static CallToolResult GetBlocks(
            [Description("softwarePath: exact PLC software path returned by discovery")] string softwarePath,
            [Description("regexName: optional regular expression applied to block names; maximum 256 characters and one-second match timeout")] string regexName = "",
            [Description("detailLevel: Summary returns identity and core PLC fields, Standard adds typed metadata, and Full adds bounded raw attributes")] ResponseDetailLevel detailLevel = ResponseDetailLevel.Summary,
            [Description("limit: maximum blocks to return. Defaults to 50 and is capped at 200")] int limit = PageRequest.DefaultLimit,
            [Description("cursor: opaque cursor returned by the previous page; omit for the first page")] string cursor = "",
            [Description("responseFormat: Auto, Toon, Csv or Json. Keep Auto unless a downstream parser requires a specific format")] ResponseFormat responseFormat = ResponseFormat.Auto)
        {
            McpResponsePresenter.ValidateRequest(
                responseFormat,
                detailLevel);
            var response = McpServer.GetBlocks(
                softwarePath,
                regexName,
                detailLevel,
                limit,
                cursor);
            var columns = detailLevel == ResponseDetailLevel.Summary
                ? BlockSummaryColumns
                : BlockStandardColumns;

            return McpResponsePresenter.Present(
                response,
                response.Items,
                response.Page,
                responseFormat,
                detailLevel,
                "items",
                columns);
        }

        [McpServerTool(Name = "GetTypes", ReadOnly = true),
         Description("List PLC data types with canonical paths, compact detail and opaque paging. Auto uses CSV for Summary, TOON for Standard and compact JSON for Full.")]
        public static CallToolResult GetTypes(
            [Description("softwarePath: exact PLC software path returned by discovery")] string softwarePath,
            [Description("regexName: optional regular expression applied to type names; maximum 256 characters and one-second match timeout")] string regexName = "",
            [Description("detailLevel: Summary returns identity and consistency, Standard adds typed metadata, and Full adds bounded raw attributes")] ResponseDetailLevel detailLevel = ResponseDetailLevel.Summary,
            [Description("limit: maximum types to return. Defaults to 50 and is capped at 200")] int limit = PageRequest.DefaultLimit,
            [Description("cursor: opaque cursor returned by the previous page; omit for the first page")] string cursor = "",
            [Description("responseFormat: Auto, Toon, Csv or Json. Keep Auto unless a downstream parser requires a specific format")] ResponseFormat responseFormat = ResponseFormat.Auto)
        {
            McpResponsePresenter.ValidateRequest(
                responseFormat,
                detailLevel);
            var response = McpServer.GetTypes(
                softwarePath,
                regexName,
                detailLevel,
                limit,
                cursor);
            var columns = detailLevel == ResponseDetailLevel.Summary
                ? TypeSummaryColumns
                : TypeStandardColumns;

            return McpResponsePresenter.Present(
                response,
                response.Items,
                response.Page,
                responseFormat,
                detailLevel,
                "items",
                columns);
        }

    }
}
