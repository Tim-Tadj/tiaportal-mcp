using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    public class TestContentPages
    {
        [TestMethod]
        public void HardwareCompilePagesRetainTargetAndHierarchy()
        {
            var report = new CompileReport { Id = "hardware", Kind = "Hardware", TargetPath = "Station/CPU", State = "Error", ErrorCount = 1,
                Diagnostics = new List<CompileDiagnostic> {
                    new CompileDiagnostic { Index = 0, Severity = "Error", Path = "Station/CPU", Description = "Invalid hardware" },
                    new CompileDiagnostic { Index = 1, ParentIndex = 0, Severity = "Error", Description = "Invalid channel" } } };
            var first = McpServer.CompilePage(report, 1, "");
            Assert.AreEqual("Hardware", first.Kind);
            Assert.AreEqual("Station/CPU", first.TargetPath);
            var second = McpServer.CompilePage(report, 1, first.Page!.NextCursor!);
            Assert.AreEqual(report.Id, second.CompileId);
            Assert.AreEqual(0, System.Linq.Enumerable.First(second.Diagnostics!).ParentIndex);
        }

        [TestMethod]
        public void ContentPagesPreserveUnicodeAndUseSnapshotBoundCursors()
        {
            var snapshot = new EngineeringContentSnapshot { Query = "block\nPLC\nMain", Text = new string('a', 255) + "\U0001F680" + new string('b', 300) };
            var first = snapshot.Page(256, "");
            Assert.AreEqual(255, first.ReturnedCharacters);
            Assert.IsTrue(first.HasMore);
            var second = snapshot.Page(1000, first.NextCursor!);
            Assert.AreEqual(snapshot.Text, first.Content + second.Content);
            Assert.IsFalse(second.HasMore);
            Assert.AreEqual(first.ContentSha256, second.ContentSha256);
            var replacement = new EngineeringContentSnapshot { Query = snapshot.Query, Text = snapshot.Text };
            Assert.ThrowsException<ArgumentException>(() => replacement.Page(256, first.NextCursor!));
        }

        [TestMethod]
        public void InvalidContentRequestsAreRejectedBeforeAnyExport()
        {
            Assert.ThrowsException<ArgumentException>(() => Portal.ValidateContentRequest(255, "SimaticML"));
            Assert.ThrowsException<ArgumentException>(() => Portal.ValidateContentRequest(16001, "SimaticML"));
            Assert.ThrowsException<ArgumentException>(() => Portal.ValidateContentRequest(1000, "Text"));
            Portal.ValidateContentRequest(1000, "Source");
        }
    }
}
