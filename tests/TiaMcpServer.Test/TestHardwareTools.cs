using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol;
using McpException = ModelContextProtocol.McpProtocolException;
using ModelContextProtocol.Server;
using System;
using System.Linq;
using TiaMcpServer.ModelContextProtocol;
using McpServer = TiaMcpServer.ModelContextProtocol.McpServer;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public class TestHardwareTools
    {
        private const string Current = "OrderNumber:6ES7 155-6AU01-0BN0/V4.2";
        private const string Replacement = "OrderNumber:6ES7 155-6AU01-0CN0/V4.2";

        [ClassInitialize]
        public static void Initialise(TestContext context)
        {
            Openness.Initialize();
        }

        [TestMethod]
        public void StaleOrEmptyExpectedTypeCannotReplaceAnotherModule()
        {
            Portal.ValidateHardwareTypeChange(Current, Current, Replacement);
            foreach (var expected in new[] { "", " ", Replacement, Current + " ", Current.Replace("V4.2", "V4.1") })
            {
                Assert.ThrowsException<ArgumentException>(() => Portal.ValidateHardwareTypeChange(Current, expected, Replacement), expected);
            }
        }

        [TestMethod]
        public void ReplacementRequiresAnOrderNumberAndFirmware()
        {
            foreach (var replacement in new[] { "", " ", "System:Device.ET200SP", "OrderNumber:", "OrderNumber:/V4.2", "OrderNumber:6ES7 155-6AU01-0CN0", "OrderNumber:6ES7 155-6AU01-0CN0/V" })
            {
                Assert.ThrowsException<ArgumentException>(() => Portal.ValidateHardwareTypeChange(Current, Current, replacement), replacement);
            }
        }

        [TestMethod]
        public void BaseUnitChangesRejectStaleOrInvalidExpectedValues()
        {
            Portal.ValidatePotentialGroupChange(0, 0);
            Portal.ValidatePotentialGroupChange(1, 1);
            Assert.ThrowsException<ArgumentException>(() => Portal.ValidatePotentialGroupChange(0, 1));
            Assert.ThrowsException<ArgumentException>(() => Portal.ValidatePotentialGroupChange(1, 0));
            Assert.ThrowsException<ArgumentException>(() => Portal.ValidatePotentialGroupChange(0, -1));
            Assert.ThrowsException<ArgumentException>(() => Portal.ValidatePotentialGroupChange(0, 2));
        }

        [TestMethod]
        public void MissingProjectCannotScanCompileOrChangeHardware()
        {
            var previous = McpServer.Portal;
            McpServer.Portal = new Portal();
            try
            {
                Assert.ThrowsException<McpException>(() => McpServer.GetAccessibleDevices("Station/CPU", "PN/IE", "wired adapter", 1));
                Assert.ThrowsException<McpException>(() => McpServer.GetHardwareNetworkInfo("Station/Module"));
                Assert.ThrowsException<McpException>(() => McpServer.SetBaseUnitPotentialGroup("Station/Module", 0, true));
                Assert.ThrowsException<McpException>(() => McpServer.CompileHardware("Station/CPU"));
                Assert.ThrowsException<McpException>(() => McpServer.ChangeHardwareType("Station/Module", Current, Replacement));
            }
            finally
            {
                McpServer.Portal = previous;
            }
        }

        [TestMethod]
        public void ToolMetadataDistinguishesReadsAndOfflineReplacement()
        {
            foreach (var name in new[] { "GetAccessibleDevices", "GetHardwareNetworkInfo" })
            {
                var tool = (McpServerToolAttribute)typeof(McpServer).GetMethod(name)!.GetCustomAttributes(typeof(McpServerToolAttribute), false).Single();
                Assert.IsTrue(tool.ReadOnly, name);
            }
            var replacement = (McpServerToolAttribute)typeof(McpServer).GetMethod("ChangeHardwareType")!.GetCustomAttributes(typeof(McpServerToolAttribute), false).Single();
            Assert.IsFalse(replacement.ReadOnly);
            Assert.IsTrue(replacement.Destructive);
        }
    }
}
