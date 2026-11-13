using System;
using System.Linq;
using System.Reflection;
using ModelContextProtocol.Server;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Runtime;
using McpServer = TiaMcpServer.ModelContextProtocol.McpServer;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    public class TestOperationDiscovery
    {
        [TestMethod]
        public void RegistryCoversEveryAdvertisedTool()
        {
            var tools = new[] { typeof(McpServer), typeof(McpListTools) }
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Select(m => new { Method = m, Tool = m.GetCustomAttribute<McpServerToolAttribute>() })
                .Where(x => x.Tool != null)
                .Select(x => x.Tool!.Name ?? x.Method.Name).ToArray();
            foreach (var name in tools)
            {
                var info = McpServer.GetOperationInfo(name).Operation;
                Assert.AreNotEqual("unclassified", info.Status, name);
                Assert.IsTrue(info.Advertised, name);
            }
            Assert.AreEqual(tools.Length, tools.Distinct(StringComparer.Ordinal).Count());
        }

        [TestMethod]
        public void VersionLimitedToolsReportUnsupportedOnOlderWorkers()
        {
            var version = WorkerBuild.Current.TiaMajorVersion;
            Assert.AreEqual(version >= 19 ? "implemented" : "unsupported-version", McpServer.GetOperationInfo("GetAccessibleDevices").Operation.Status);
            Assert.AreEqual(version >= 18 ? "implemented" : "unsupported-version", McpServer.GetOperationInfo("FindReferences").Operation.Status);
        }

        [TestMethod]
        public void DetailsDescribeRequiredAndOptionalParameters()
        {
            var details = McpServer.GetOperationInfo("GetOperationInfo");
            Assert.AreEqual("name", details.Parameters[0].Name);
            Assert.IsTrue(details.Parameters[0].Required);
            var list = McpServer.GetOperationInfo("GetOperations");
            Assert.IsFalse(list.Parameters.First(p => p.Name == "limit").Required);
        }

        [TestMethod]
        public void ParameterTypesIncludeNullableAndArrayElements()
        {
            Assert.AreEqual("Int32?", OperationRegistry.FriendlyType(typeof(int?)));
            Assert.AreEqual("String[]", OperationRegistry.FriendlyType(typeof(string[])));
            Assert.AreEqual("List<Int32>", OperationRegistry.FriendlyType(typeof(System.Collections.Generic.List<int>)));
        }

        [TestMethod]
        public void NativeReferencesRejectReplacementEvenWhenNamesMatch()
        {
            var original = new object();
            var replacement = new object();
            Assert.IsTrue(Portal.SameNativeObject(original, original));
            Assert.IsFalse(Portal.SameNativeObject(original, replacement));
        }

        [TestMethod]
        public void ConnectionAndProjectReadsHaveDistinctEffects()
        {
            Assert.AreEqual(false, McpServer.GetOperationInfo("Connect").Operation.ReadOnly);
            Assert.AreEqual(true, McpServer.GetOperationInfo("GetState").Operation.ReadOnly);
            Assert.IsTrue(McpServer.GetOperationInfo("Connect").Operation.Effect.Contains("connection"));
        }

        [TestMethod]
        public void PublicApiRoutesAreClassifiedAndNativeEffectsAreConservative()
        {
            Assert.AreEqual("public-api", McpServer.GetOperationInfo("DescribeApiObject").Operation.Domain);
            Assert.AreEqual(true, McpServer.GetOperationInfo("ReadApiProperty").Operation.ReadOnly);
            if (typeof(McpServer).GetMethod("InvokeApiMethod") == null) return;
            var native = McpServer.GetOperationInfo("InvokeApiMethod").Operation;
            Assert.AreEqual("ReadWrite", native.Profile);
            Assert.AreEqual(false, native.ReadOnly);
            Assert.IsTrue(native.NativeEffectsUnclassified);
            Assert.IsTrue(native.WritesFiles && native.ModifiesProject && native.ChangesEngineeringConnection && native.ChangesPlc);
        }
    }
}
