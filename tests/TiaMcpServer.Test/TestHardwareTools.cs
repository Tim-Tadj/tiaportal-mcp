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
        public void SoftwareVersionsUseTheExactSiemensCatalogueSyntax()
        {
            const string current = "OrderNumber:OPC Server/SW V19 ...";
            Portal.ValidateHardwareTypeChange(current, current, "OrderNumber:OPC Server/SW V17 ...");
            Portal.ValidateHardwareTypeChange(current, current, "OrderNumber:OPC Server/SW V17.0 ...");
            Assert.ThrowsException<ArgumentException>(() => Portal.ValidateHardwareTypeChange(current, "OrderNumber:OPC Server/SW V18 ...", "OrderNumber:OPC Server/SW V17 ..."));
            foreach (var invalid in new[] { "OrderNumber:/SW V17 ...", "OrderNumber:OPC Server/SW V ...", "OrderNumber:OPC Server/SW V17", "OrderNumber:OPC Server/SW V17 ...extra", "OrderNumber:OPC Server/SW V17 ...\n" })
                Assert.ThrowsException<ArgumentException>(() => Portal.ValidateHardwareTypeChange(current, current, invalid), invalid);
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
        public void HardwareChangesRejectStaleValuesAndNonCanonicalAddresses()
        {
            Portal.ValidateExactValue("Input", "Input", "direction");
            Assert.ThrowsException<ArgumentException>(() => Portal.ValidateExactValue("Input", "Output", "direction"));
            Assert.ThrowsException<ArgumentException>(() => Portal.ValidateExactValue("Input", null!, "direction"));
            Portal.ValidateIpv4("192.168.1.10", "address");
            foreach (var value in new[] { "192.168.001.10", "::1", "192.168.1.256", " 192.168.1.10" })
                Assert.ThrowsException<ArgumentException>(() => Portal.ValidateIpv4(value, "address"), value);
            Assert.ThrowsException<ArgumentException>(() => Portal.RequireHardwareName(" module ", "module name"));
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
