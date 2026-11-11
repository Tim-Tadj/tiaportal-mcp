using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TiaMcp.Contracts;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;
using McpServer = TiaMcpServer.ModelContextProtocol.McpServer;

namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public class TestIssue1
    {
        [ClassInitialize]
        public static void Initialise(TestContext context) { Openness.Initialize(); }

        [DataTestMethod]
        [DataRow(ResponseFormat.Auto, ResponseDetailLevel.Summary)]
        [DataRow(ResponseFormat.Toon, ResponseDetailLevel.Standard)]
        [DataRow(ResponseFormat.Csv, ResponseDetailLevel.Standard)]
        [DataRow(ResponseFormat.Json, ResponseDetailLevel.Summary)]
        [DataRow(ResponseFormat.Json, ResponseDetailLevel.Full)]
        public void FirstContentBlockContainsRowsAndPaging(ResponseFormat format, ResponseDetailLevel detail)
        {
            var rows = new[] { new ResponseBlockInfo { Name = "Pump_Unicode_Ã©", Path = "Group/Pump_Unicode_Ã©" } };
            var page = new PageInfo { Returned = 1, HasMore = true, NextCursor = "continuation" };
            var result = McpResponsePresenter.Present(new ResponseBlocks { Items = rows, Page = page }, rows, page,
                format, detail, "items", new[] { new TabularColumn<ResponseBlockInfo>("name", r => TabularCell.From(r.Name)),
                    new TabularColumn<ResponseBlockInfo>("path", r => TabularCell.From(r.Path)) });
            Assert.AreEqual(1, result.Content.Count);
            Assert.IsNull(result.StructuredContent);
            var text = ((TextContentBlock)result.Content[0]).Text;
            var separator = text.IndexOf('\n');
            using (var metadata = JsonDocument.Parse(text.Substring(0, separator)))
            {
                Assert.AreEqual(1, metadata.RootElement.GetProperty("returned").GetInt32());
                Assert.IsTrue(metadata.RootElement.GetProperty("hasMore").GetBoolean());
                Assert.AreEqual("continuation", metadata.RootElement.GetProperty("nextCursor").GetString());
            }
            var payload = text.Substring(separator + 1);
            if (format == ResponseFormat.Json)
            {
                using (var json = JsonDocument.Parse(payload))
                    Assert.AreEqual(rows[0].Name, json.RootElement.GetProperty("items")[0].GetProperty("name").GetString());
            }
            else StringAssert.Contains(payload, rows[0].Name);
        }

        [TestMethod]
        public void EmptyToonKeepsColumnDeclarationAndAnEmptyPayload()
        {
            var rows = Array.Empty<ResponseBlockInfo>();
            var result = McpResponsePresenter.Present(new ResponseBlocks { Items = rows }, rows, null,
                ResponseFormat.Toon, ResponseDetailLevel.Standard, "items",
                new[] { new TabularColumn<ResponseBlockInfo>("name", r => TabularCell.From(r.Name)) });
            var text = ((TextContentBlock)result.Content[0]).Text;
            StringAssert.Contains(text, "\"columns\":[\"name\"]");
            StringAssert.EndsWith(text, "\nitems: []");
        }

        [TestMethod]
        public void PagingCannotSkipOrReuseAnotherQueriesCursor()
        {
            var rows = Enumerable.Range(0, 105).ToArray();
            var first = McpServer.PageItems(rows, 50, "", "compile:one", out var page);
            CollectionAssert.AreEqual(Enumerable.Range(0, 50).ToArray(), first.ToArray());
            Assert.IsTrue(page.HasMore);
            Assert.ThrowsException<ArgumentException>(() => McpServer.PageItems(rows, 50, page.NextCursor!, "compile:two", out _));
            var second = McpServer.PageItems(rows, 50, page.NextCursor!, "compile:one", out var next);
            var third = McpServer.PageItems(rows, 50, next.NextCursor!, "compile:one", out var last);
            CollectionAssert.AreEqual(rows, first.Concat(second).Concat(third).ToArray());
            Assert.IsFalse(last.HasMore);
            Assert.IsNull(last.NextCursor);
            Assert.ThrowsException<ArgumentException>(() => McpServer.PageItems(rows, 50, PageRequest.CreateCursor(999, "compile:one"), "compile:one", out _));
        }

        [TestMethod]
        public void ErrorCompileReturnsDiagnosticsAndSubsequentPages()
        {
            var report = new CompileReport { Id = "compile-id", State = "Error", ErrorCount = 2, WarningCount = 1,
                Diagnostics = new List<CompileDiagnostic> {
                    new CompileDiagnostic { Index = 0, Severity = "Error", Path = "FC10", Description = "Unknown symbol" },
                    new CompileDiagnostic { Index = 1, ParentIndex = 0, Severity = "Error", Path = "FC10/statement", Description = "Type mismatch" } } };
            var first = McpServer.CompilePage(report, 1, "");
            Assert.AreEqual("Error", first.State);
            Assert.AreEqual(2, first.ErrorCount);
            Assert.AreEqual("Unknown symbol", first.Diagnostics!.Single().Description);
            var second = McpServer.CompilePage(report, 1, first.Page!.NextCursor!);
            Assert.AreEqual(0, second.Diagnostics!.Single().ParentIndex);
            Assert.IsFalse(second.Page!.HasMore);
        }

        [TestMethod]
        public void SourceImportRejectsUnacknowledgedReplacementAndWrongOrMissingFiles()
        {
            Assert.ThrowsException<ArgumentException>(() => Portal.ValidateSourceFile("test.scl", false));
            Assert.ThrowsException<ArgumentException>(() => Portal.ValidateSourceFile("test.xml", true));
            Assert.ThrowsException<ArgumentException>(() => Portal.ValidateSourceFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".scl"), true));
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".scl");
            try
            {
                File.WriteAllText(path, "");
                Assert.ThrowsException<ArgumentException>(() => Portal.ValidateSourceFile(path, true));
                File.WriteAllText(path, "FUNCTION test : Void BEGIN END_FUNCTION");
                Portal.ValidateSourceFile(path, true);
                File.WriteAllBytes(path, new byte[2*1024*1024+1]);
                Assert.ThrowsException<ArgumentException>(() => Portal.ValidateSourceFile(path, true));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [DataTestMethod]
        [DataRow("Program blocks", "")]
        [DataRow("Program blocks/Pumps/Area", "Pumps/Area")]
        [DataRow("Pumps/Area", "Pumps/Area")]
        public void CanonicalAndLegacyGroupPathsResolveToTheSameChildGroups(string path, string expected)
        {
            Assert.AreEqual(expected, string.Join("/", Portal.ChildGroupNames(path, "Program blocks")));
        }

        [TestMethod]
        public void DisconnectedPortalCannotReturnStaleCompileResults()
        {
            var portal = new Portal();
            try
            {
                Assert.IsFalse(portal.GetState().IsConnected);
                Assert.ThrowsException<ArgumentException>(() => portal.GetCompileReport("stale"));
            }
            finally { portal.Dispose(); }
        }
    }
}
