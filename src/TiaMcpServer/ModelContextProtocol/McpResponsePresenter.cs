using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using TiaMcp.Contracts;

namespace TiaMcpServer.ModelContextProtocol
{
    internal static class McpResponsePresenter
    {
        public static void ValidateRequest(
            ResponseFormat requestedFormat,
            ResponseDetailLevel detailLevel)
        {
            var presentationDetailLevel = ToPresentationDetailLevel(detailLevel);

            if (presentationDetailLevel == PresentationDetailLevel.Full &&
                (requestedFormat == ResponseFormat.Toon ||
                 requestedFormat == ResponseFormat.Csv))
            {
                throw new McpException(
                    $"responseFormat {requestedFormat} cannot represent detailLevel Full " +
                    "without losing nested attributes. Use responseFormat Json, or use " +
                    "detailLevel Summary or Standard.",
                    McpErrorCode.InvalidParams);
            }

            try
            {
                ResponseFormatSelector.Select(
                    requestedFormat,
                    presentationDetailLevel,
                    presentationDetailLevel != PresentationDetailLevel.Full);
            }
            catch (Exception ex) when (
                ex is ArgumentException ||
                ex is InvalidOperationException)
            {
                throw new McpException(
                    $"Invalid response format request: {ex.Message}",
                    ex,
                    McpErrorCode.InvalidParams);
            }
        }

        public static CallToolResult Present<TResponse, TItem>(
            TResponse response,
            IEnumerable<TItem>? items,
            PageInfo? page,
            ResponseFormat requestedFormat,
            ResponseDetailLevel detailLevel,
            string tableName,
            IReadOnlyList<TabularColumn<TItem>> columns)
        {
            if (response is null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (columns == null)
            {
                throw new ArgumentNullException(nameof(columns));
            }

            var rows = items?.ToList() ?? new List<TItem>();
            var presentationDetailLevel = ToPresentationDetailLevel(detailLevel);
            var isTabularEligible =
                presentationDetailLevel != PresentationDetailLevel.Full &&
                rows.Count <= TabularPresentation.MaximumRows &&
                columns.Count > 0 &&
                columns.Count <= TabularPresentation.MaximumColumns &&
                rows.All(item => item is not null);
            var resolvedFormat = SelectFormat(
                requestedFormat,
                presentationDetailLevel,
                isTabularEligible,
                rows.Count,
                columns.Count);
            string payload;

            try
            {
                payload = CreatePayload(
                    response,
                    rows,
                    resolvedFormat,
                    tableName,
                    columns);
            }
            catch (Exception ex) when (
                resolvedFormat != ResponseFormat.Json &&
                IsTabularPresentationException(ex))
            {
                if (requestedFormat == ResponseFormat.Auto)
                {
                    resolvedFormat = ResponseFormat.Json;
                    payload = JsonSerializer.Serialize(
                        response,
                        McpJsonUtilities.DefaultOptions);
                }
                else
                {
                    throw new McpException(
                        $"responseFormat {requestedFormat} cannot represent this " +
                        $"result losslessly: {ex.Message} Use responseFormat Auto " +
                        "or Json.",
                        ex,
                        McpErrorCode.InvalidParams);
                }
            }

            var returned = rows.Count;
            var hasMore = page?.HasMore ?? false;
            var nextCursor = page?.NextCursor;
            var formatName = GetFormatName(resolvedFormat);
            var structuredContent = new JsonObject
            {
                ["format"] = formatName,
                ["returned"] = returned,
                ["hasMore"] = hasMore,
                ["nextCursor"] = nextCursor
            };

            if (resolvedFormat == ResponseFormat.Toon && returned == 0)
            {
                var columnNames = new JsonArray();
                foreach (var column in columns)
                {
                    columnNames.Add(JsonValue.Create(column.Name));
                }

                structuredContent["columns"] = columnNames;
            }

            return new CallToolResult
            {
                Content = new List<ContentBlock>
                {
                    new TextContentBlock
                    {
                        Text = structuredContent.ToJsonString(
                            McpJsonUtilities.DefaultOptions)
                    },
                    new TextContentBlock
                    {
                        Text = payload
                    }
                },
                StructuredContent = structuredContent
            };
        }

        private static string CreatePayload<TResponse, TItem>(
            TResponse response,
            IReadOnlyList<TItem> rows,
            ResponseFormat resolvedFormat,
            string tableName,
            IReadOnlyList<TabularColumn<TItem>> columns)
        {
            if (resolvedFormat == ResponseFormat.Json)
            {
                return JsonSerializer.Serialize(
                    response,
                    McpJsonUtilities.DefaultOptions);
            }

            var presentation = TabularPresentation.Create(
                tableName,
                rows,
                columns);

            switch (resolvedFormat)
            {
                case ResponseFormat.Toon:
                    return ToonTableWriter.Write(presentation);
                case ResponseFormat.Csv:
                    return CsvTableWriter.Write(presentation);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(resolvedFormat),
                        resolvedFormat,
                        "Unsupported resolved response format.");
            }
        }

        private static ResponseFormat SelectFormat(
            ResponseFormat requestedFormat,
            PresentationDetailLevel detailLevel,
            bool isTabularEligible,
            int rowCount,
            int columnCount)
        {
            try
            {
                return ResponseFormatSelector.Select(
                    requestedFormat,
                    detailLevel,
                    isTabularEligible);
            }
            catch (InvalidOperationException ex)
            {
                throw new McpException(
                    $"responseFormat {requestedFormat} cannot represent this result " +
                    $"within the bounded table profile ({rowCount.ToString(CultureInfo.InvariantCulture)} " +
                    $"rows, {columnCount.ToString(CultureInfo.InvariantCulture)} columns; " +
                    $"limits are {TabularPresentation.MaximumRows.ToString(CultureInfo.InvariantCulture)} " +
                    $"rows and {TabularPresentation.MaximumColumns.ToString(CultureInfo.InvariantCulture)} " +
                    "columns). Use responseFormat Auto or Json.",
                    ex,
                    McpErrorCode.InvalidParams);
            }
            catch (ArgumentException ex)
            {
                throw new McpException(
                    $"Invalid response format request: {ex.Message}",
                    ex,
                    McpErrorCode.InvalidParams);
            }
        }

        private static string GetFormatName(ResponseFormat format)
        {
            switch (format)
            {
                case ResponseFormat.Toon:
                    return "toon-v4.1";
                case ResponseFormat.Csv:
                    return "csv";
                case ResponseFormat.Json:
                    return "json";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(format),
                        format,
                        "Unsupported resolved response format.");
            }
        }

        private static bool IsTabularPresentationException(Exception exception)
        {
            return exception is ArgumentException ||
                exception is InvalidOperationException;
        }

        private static PresentationDetailLevel ToPresentationDetailLevel(
            ResponseDetailLevel detailLevel)
        {
            switch (detailLevel)
            {
                case ResponseDetailLevel.Summary:
                    return PresentationDetailLevel.Summary;
                case ResponseDetailLevel.Standard:
                    return PresentationDetailLevel.Standard;
                case ResponseDetailLevel.Full:
                    return PresentationDetailLevel.Full;
                default:
                    throw new McpException(
                        $"Unsupported detailLevel value '{detailLevel}'.",
                        McpErrorCode.InvalidParams);
            }
        }
    }
}
