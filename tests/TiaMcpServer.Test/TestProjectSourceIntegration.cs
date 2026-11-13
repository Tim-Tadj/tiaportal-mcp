using Microsoft.VisualStudio.TestTools.UnitTesting;
using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW.ExternalSources;
using System;
using System.IO;
using System.Collections.Generic;
using TiaMcpServer.Security;
using System.Linq;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;
using McpServer = TiaMcpServer.ModelContextProtocol.McpServer;

namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public class TestProjectSourceIntegration
    {
        [TestMethod]
        [TestCategory("TIAIntegration")]
        public void DisposableProjectSupportsSourceDiagnosticsAndRepeatedQueries()
        {
            var path = Environment.GetEnvironmentVariable("TIA_MCP_DISPOSABLE_PROJECT");
            if (string.IsNullOrEmpty(path)) Assert.Inconclusive("Set TIA_MCP_DISPOSABLE_PROJECT to an explicitly disposable .ap19 copy. Never use a working project.");
            if (!File.Exists(Path.Combine(Path.GetDirectoryName(path!)!, ".tia-mcp-disposable-test")))
                Assert.Inconclusive("The project folder must contain .tia-mcp-disposable-test to authorise this destructive fixture.");
            var softwarePath = Environment.GetEnvironmentVariable("TIA_MCP_TEST_SOFTWARE_PATH");
            var referenceSearch = Environment.GetEnvironmentVariable("TIA_MCP_TEST_REFERENCE");
            if (string.IsNullOrEmpty(softwarePath) || string.IsNullOrEmpty(referenceSearch))
                Assert.Inconclusive("Set TIA_MCP_TEST_SOFTWARE_PATH and TIA_MCP_TEST_REFERENCE for the disposable fixture.");
            Openness.Initialize();
            var previous = McpServer.Portal;
            var wrapper = new Portal();
            using (var tia = new TiaPortal(TiaPortalMode.WithoutUserInterface))
            {
                try
                {
                    var project = tia.Projects.Open(new FileInfo(path!));
                    Assert.IsTrue(wrapper.ConnectPortal(false, tia.GetCurrentProcess().Id));
                    McpServer.Portal = wrapper;
                    File.AppendAllText(path + ".test-progress", "Attached\n");
                    Console.WriteLine("Owned TIA process: " + tia.GetCurrentProcess().Id);
                    var plc = wrapper.GetPlcSoftware(softwarePath);
                    Assert.IsNotNull(plc, "No PLC software at the configured fixture path.");
                    Console.WriteLine("Software: " + softwarePath);
                    for (var i = 0; i < 4; i++)
                    {
                        Assert.IsTrue(McpServer.GetState().IsConnected);
                        Assert.IsTrue(McpServer.GetBlocks(softwarePath, limit: 2).Items!.Any());
                        Assert.IsTrue(McpServer.GetProjects().Items!.Any());
                    }
                    var device = wrapper.GetDevice(softwarePath!.Substring(0, softwarePath.LastIndexOf('/')));
                    Assert.IsNotNull(device);
                    Assert.IsTrue(wrapper.GetDeviceModuleSummary(device!).Length > 0);
                    var inventoryName = "MCP_Ungrouped_" + Guid.NewGuid().ToString("N");
                    var inventoryDevice = project.UngroupedDevicesGroup.Devices.CreateWithItem(
                        "OrderNumber:6ES7 155-6AU01-0BN0/V4.2", inventoryName + "_Head", inventoryName);
                    try
                    {
                        var canonical = wrapper.GetDevicePath(inventoryDevice);
                        Assert.AreEqual("UngroupedDevices/" + inventoryName, canonical);
                        Assert.IsTrue(wrapper.GetDevices().Any(candidate => Portal.SameNativeObject(candidate, inventoryDevice)));
                        Assert.IsTrue(Portal.SameNativeObject(inventoryDevice, wrapper.GetDevice(canonical)!));
                        Assert.IsNull(wrapper.GetDevice(canonical + "/not-a-device"));
                    }
                    finally { inventoryDevice.Delete(); }
                    var projectInfo = JsonSerializer.Serialize(wrapper.GetProjectInfo());
                    StringAssert.Contains(projectInfo, ".ap19");
                    var tree = McpServer.GetSoftwareTree(softwarePath, 3);
                    Assert.AreEqual(3, tree.Page!.Returned);
                    Assert.IsTrue(tree.Page.HasMore);
                    Assert.IsTrue(McpServer.GetSoftwareTree(softwarePath, 3, tree.Page.NextCursor!).Page!.Returned > 0);
                    var tables = wrapper.GetPlcTagTables(softwarePath);
                    Assert.IsTrue(tables.Count > 0);
                    Assert.IsTrue(wrapper.GetPlcTags(softwarePath, "").Count > 0);
                    Console.WriteLine("Tags: " + wrapper.GetPlcTags(softwarePath, "").Count);
                    File.AppendAllText(path + ".test-progress", "Querying references\n");
                    var references = wrapper.FindReferences(softwarePath, referenceSearch!);
                    Assert.IsTrue(references.Count > 0, "Native cross-reference service returned no location rows.");
                    Console.WriteLine("Native reference locations: " + references.Count);
                    File.AppendAllText(path + ".test-progress", "Generating source\n");
                    var sourcePath = Path.Combine(Path.GetDirectoryName(path!)!, "MCP_Issue1_Add.scl");
                    File.WriteAllText(sourcePath, "FUNCTION \"MCP_Issue1_Add\" : Int\nVERSION : 0.1\nVAR_INPUT\n a : Int;\n b : Int;\nEND_VAR\nBEGIN\n #MCP_Issue1_Add := #a + #b;\nEND_FUNCTION", new UTF8Encoding(true));
                    McpServer.ImportBlockFromSource(softwarePath, sourcePath, true);
                    Assert.AreEqual(1, wrapper.GetBlocks(softwarePath, "^MCP_Issue1_Add$").Count);
                    File.AppendAllText(path + ".test-progress", "Compiling good source\n");
                    var compiled = McpServer.CompileSoftware(softwarePath, limit: 1);
                    Assert.IsNotNull(compiled.State);
                    Assert.AreEqual(0, compiled.ErrorCount, "The valid SCL fixture must compile successfully.");
                    if (compiled.Page!.HasMore)
                        Assert.IsTrue(McpServer.GetCompileDiagnostics(compiled.CompileId!, 1, compiled.Page.NextCursor!).Page!.Returned > 0);
                    var badPath = Path.Combine(Path.GetDirectoryName(path!)!, "MCP_Issue1_Bad.scl");
                    File.WriteAllText(badPath, "FUNCTION \"MCP_Issue1_Bad\" : Int\nVERSION : 0.1\nBEGIN\n \"MCP_Issue1_Bad\" := \"MCP_Unknown_Symbol\";\nEND_FUNCTION", new UTF8Encoding(true));
                    var bad = plc.ExternalSourceGroup.ExternalSources.CreateFromFile("MCP_Issue1_Bad.scl", badPath);
                    bad.GenerateBlocksFromSource(GenerateBlockOption.KeepOnError);
                    File.AppendAllText(path + ".test-progress", "Compiling error fixture\n");
                    var failed = McpServer.CompileSoftware(softwarePath, limit: 200);
                    Console.WriteLine("Error fixture compile: " + JsonSerializer.Serialize(failed));
                    Assert.IsTrue(failed.ErrorCount > 0);
                    Assert.AreEqual("Error", failed.State);
                    Assert.IsTrue(failed.Diagnostics!.Any(d => d.Severity == "Error"));
                    Assert.IsTrue(failed.Diagnostics!.All(d => d.Description != null));
                    Assert.IsTrue(McpServer.GetState().IsConnected);
                    var badBlock = wrapper.GetBlocks(softwarePath!, "^MCP_Issue1_Bad$").Single();
                    var error = Assert.ThrowsException<PortalException>(() => wrapper.ExportBlock(softwarePath!, wrapper.GetBlockPath(badBlock), ""));
                    StringAssert.Contains(error.Message, "Compile the PLC software");
                    var failures = new List<BlockExportFailure>();
                    Assert.AreEqual(0, wrapper.ExportBlocks(softwarePath!, "", "^MCP_Issue1_Bad$", failureDetails: failures)!.Count());
                    Assert.AreEqual(1, failures.Count);
                    StringAssert.Contains(failures[0].Reason!, "Compile the PLC software");
                    project.Close();
                    Assert.AreEqual("-", McpServer.GetState().Project);
                    var secondDirectory = Path.Combine(Path.GetDirectoryName(path!)!, "second-project-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(secondDirectory);
                    var second = tia.Projects.Create(new DirectoryInfo(secondDirectory), "MCP_Empty");
                    Assert.AreEqual("MCP_Empty", McpServer.GetState().Project);
                    second.Close();
                }
                finally
                {
                    wrapper.Dispose();
                    McpServer.Portal = previous;
                }
            }
        }
    }
}
