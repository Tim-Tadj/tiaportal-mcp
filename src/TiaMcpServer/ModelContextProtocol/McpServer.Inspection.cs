using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using TiaMcp.Contracts;
using TiaMcpServer.Security;
using TiaMcpServer.Siemens;
using McpException = ModelContextProtocol.McpProtocolException;

namespace TiaMcpServer.ModelContextProtocol
{
    public sealed partial class McpServer
    {
        internal static IReadOnlyList<T> PageItems<T>(IReadOnlyList<T> items, int limit, string cursor, string scope, out PageInfo page)
        {
            var request = new PageRequest { Limit = limit, Cursor = cursor };
            var offset = request.GetOffset(scope);
            if (offset > items.Count) throw new ArgumentException("The cursor is outside the available results.");
            var rows = items.Skip(offset).Take(request.GetBoundedLimit()).ToList();
            var hasMore = offset + rows.Count < items.Count;
            page = new PageInfo
            {
                Returned = rows.Count, HasMore = hasMore,
                NextCursor = hasMore ? PageRequest.CreateCursor(offset + rows.Count, scope) : null
            };
            return rows;
        }

        private static T ProjectCall<T>(Func<T> action)
        {
            try { return action(); }
            catch (McpException) { throw; }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is RegexMatchTimeoutException)
            {
                throw new McpException(ex.Message, ex, McpErrorCode.InvalidParams);
            }
            catch (Exception ex)
            {
                throw new McpException("TIA project request failed: " + ex.Message, ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetPlcTagTables", ReadOnly = true), Description("List paged PLC tag tables with canonical paths and tag/constant counts. Read only.")]
        public static CallToolResult GetPlcTagTables(string softwarePath, int limit = PageRequest.DefaultLimit, string cursor = "", ResponseFormat responseFormat = ResponseFormat.Auto)
        {
            return ProjectCall(() =>
            {
                McpResponsePresenter.ValidateRequest(responseFormat, ResponseDetailLevel.Summary);
                var rows = PageItems(Portal.GetPlcTagTables(softwarePath), limit, cursor,
                    "tag-tables:" + softwarePath, out var page);
                return McpResponsePresenter.Present(new PagedResponse<PlcTagTableEntry> { Items = rows, Page = page }, rows, page,
                    responseFormat, ResponseDetailLevel.Summary, "items", new[]
                    {
                        new TabularColumn<PlcTagTableEntry>("path", t => TabularCell.From(t.Path)),
                        new TabularColumn<PlcTagTableEntry>("name", t => TabularCell.From(t.Name)),
                        new TabularColumn<PlcTagTableEntry>("tagCount", t => TabularCell.From(t.TagCount)),
                        new TabularColumn<PlcTagTableEntry>("constantCount", t => TabularCell.From(t.ConstantCount))
                    });
            });
        }

        [McpServerTool(Name = "GetPlcTags", ReadOnly = true), Description("List PLC tags and user constants with table paths, data types, logical addresses or constant values. Paged; tablePath comes from GetPlcTagTables, regexName filters symbol names.")]
        public static CallToolResult GetPlcTags(string softwarePath, string tablePath = "", string regexName = "", int limit = PageRequest.DefaultLimit, string cursor = "", ResponseFormat responseFormat = ResponseFormat.Auto)
        {
            return ProjectCall(() =>
            {
                McpResponsePresenter.ValidateRequest(responseFormat, ResponseDetailLevel.Standard);
                var filter = string.IsNullOrEmpty(regexName) ? null : BoundedRegex.Create(regexName, RegexOptions.IgnoreCase);
                var entries = Portal.GetPlcTags(softwarePath, tablePath).Where(t => filter == null || filter.IsMatch(t.Name ?? "")).ToList();
                var rows = PageItems(entries, limit, cursor, "tags:" + softwarePath + "\n" + tablePath + "\n" + regexName, out var page);
                return McpResponsePresenter.Present(new PagedResponse<PlcTagEntry> { Items = rows, Page = page }, rows, page,
                    responseFormat, ResponseDetailLevel.Standard, "items", new[]
                    {
                        new TabularColumn<PlcTagEntry>("tablePath", t => TabularCell.From(t.TablePath)),
                        new TabularColumn<PlcTagEntry>("name", t => TabularCell.From(t.Name)),
                        new TabularColumn<PlcTagEntry>("kind", t => TabularCell.From(t.Kind)),
                        new TabularColumn<PlcTagEntry>("dataType", t => TabularCell.From(t.DataType)),
                        new TabularColumn<PlcTagEntry>("address", t => TabularCell.From(t.Address)),
                        new TabularColumn<PlcTagEntry>("value", t => TabularCell.From(t.Value))
                    });
            });
        }

        [McpServerTool(Name = "FindReferences", ReadOnly = true), Description("Query Siemens native cross-references in V18/V19. Search selects an exact block name/path, DB number or tag symbol/address, with native access and location fields. No text-search approximation. Results depend on TIA's reference data and do not prove absence of indirect accesses. Unsupported in V17.")]
        public static CallToolResult FindReferences(string softwarePath, string search, int limit = PageRequest.DefaultLimit, string cursor = "", ResponseFormat responseFormat = ResponseFormat.Auto)
        {
            return ProjectCall(() =>
            {
                if (string.IsNullOrWhiteSpace(search) || search.Length > 256)
                    throw new ArgumentException("Supply a symbol, path or address search of 1 to 256 characters.");
                McpResponsePresenter.ValidateRequest(responseFormat, ResponseDetailLevel.Standard);
                var matches = Portal.FindReferences(softwarePath, search);
                var rows = PageItems(matches, limit, cursor, "references:" + softwarePath + "\n" + search, out var page);
                return McpResponsePresenter.Present(new PagedResponse<PlcReferenceEntry> { Items = rows, Page = page }, rows, page,
                    responseFormat, ResponseDetailLevel.Standard, "items", new[]
                    {
                        new TabularColumn<PlcReferenceEntry>("sourcePath", r => TabularCell.From(r.SourcePath)),
                        new TabularColumn<PlcReferenceEntry>("sourceName", r => TabularCell.From(r.SourceName)),
                        new TabularColumn<PlcReferenceEntry>("sourceAddress", r => TabularCell.From(r.SourceAddress)),
                        new TabularColumn<PlcReferenceEntry>("targetPath", r => TabularCell.From(r.TargetPath)),
                        new TabularColumn<PlcReferenceEntry>("targetName", r => TabularCell.From(r.TargetName)),
                        new TabularColumn<PlcReferenceEntry>("targetAddress", r => TabularCell.From(r.TargetAddress)),
                        new TabularColumn<PlcReferenceEntry>("access", r => TabularCell.From(r.Access)),
                        new TabularColumn<PlcReferenceEntry>("referenceType", r => TabularCell.From(r.ReferenceType)),
                        new TabularColumn<PlcReferenceEntry>("location", r => TabularCell.From(r.Location)),
                        new TabularColumn<PlcReferenceEntry>("referencedAs", r => TabularCell.From(r.ReferencedAs))
                    });
            });
        }

#if TIA_MCP_READ_WRITE
        [McpServerTool(Name = "ImportBlockFromSource", ReadOnly = false, Destructive = true), Description("Generate offline blocks/types from a .scl, .awl or .db source file using Siemens external sources. Can replace existing blocks, including on partial failure, so acceptBlockReplacement must be explicitly true. No save or download. Compile and inspect generated blocks before saving.")]
        public static object ImportBlockFromSource(string softwarePath, string sourcePath, bool acceptBlockReplacement)
        {
            return ProjectCall(() => Portal.ImportBlockFromSource(softwarePath, sourcePath, acceptBlockReplacement));
        }
#endif
    }
}
