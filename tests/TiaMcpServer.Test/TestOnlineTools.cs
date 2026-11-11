using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol;
using McpException = ModelContextProtocol.McpProtocolException;
using ModelContextProtocol.Server;
using Siemens.Engineering.Online;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using TiaMcpServer.ModelContextProtocol;
using McpServer = TiaMcpServer.ModelContextProtocol.McpServer;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public class TestOnlineTools
    {
        [ClassInitialize]
        public static void Initialise(TestContext context)
        {
            Openness.Initialize();
        }

        [TestMethod]
        public void BuildRejectsAnotherTiaMajorBeforeConnecting()
        {
            BuildVersion.Validate(BuildVersion.TiaMajorVersion);
            foreach (var other in new[] { 19, 20, 21 }.Where(v => v != BuildVersion.TiaMajorVersion))
            {
                Assert.ThrowsException<ArgumentException>(() => BuildVersion.Validate(other));
            }
        }

        [TestMethod]
        public void V21ResolverSelectsNetFrameworkApiOnly()
        {
            foreach (var major in new[] { 19, 20 })
            {
                CollectionAssert.AreEqual(new[] { Path.Combine("test-root", "PublicAPI", "V" + major), Path.Combine("test-root", "Bin", "PublicAPI") }, Engineering.GetApiSearchDirectories("test-root", major));
            }
            CollectionAssert.AreEqual(new[] { Path.Combine("test-root", "PublicAPI", "V21", "net48") }, Engineering.GetApiSearchDirectories("test-root", 21));
        }

        [TestMethod]
        public void InvalidProcessSelectionIsRejectedBeforeAttaching()
        {
            var portal = new Portal();
            Assert.ThrowsException<ArgumentException>(() => portal.ConnectPortal(false, 0));
            Assert.ThrowsException<ArgumentException>(() => portal.ConnectPortal(false, -1));
            Assert.IsFalse(portal.IsConnected());
        }

        [TestMethod]
        public void OnlineTargetsRequireFullUnambiguousPaths()
        {
            Portal.ValidateOnlineDevicePath("Group/Station/Rack/CPU");
            foreach (var invalid in new[] { "", "CPU", "/Station/CPU", "Station/CPU/", "Station//CPU", "Station/../CPU", "Station/./CPU", "Station/ /CPU" })
            {
                var error = Assert.ThrowsException<PortalException>(() => Portal.ValidateOnlineDevicePath(invalid));
                Assert.AreEqual(PortalErrorCode.InvalidParams, error.Code, invalid);
            }
        }

        [TestMethod]
        public void InvalidConnectionsAreRejectedWithoutTia()
        {
            Portal.ValidateOnlineConnection("PN/IE", "test NIC", 1, "10.72.72.69", 30);
            foreach (var ip in new[] { "", "10.69", "010.072.072.069", "10.72.72.999", "::1", "hostname", "0.0.0.0", "255.255.255.255" })
            {
                Assert.ThrowsException<PortalException>(() => Portal.ValidateOnlineConnection("PN/IE", "NIC", 1, ip, 30), ip);
            }
            Assert.ThrowsException<PortalException>(() => Portal.ValidateOnlineConnection("", "NIC", 1, "10.72.72.69", 30));
            Assert.ThrowsException<PortalException>(() => Portal.ValidateOnlineConnection("PN/IE", "", 1, "10.72.72.69", 30));
            Assert.ThrowsException<PortalException>(() => Portal.ValidateOnlineConnection("PN/IE", "NIC", -1, "10.72.72.69", 30));
            Assert.ThrowsException<PortalException>(() => Portal.ValidateOnlineConnection("PN/IE", "NIC", 1, "10.72.72.69", 0));
            Assert.ThrowsException<PortalException>(() => Portal.ValidateOnlineConnection("PN/IE", "NIC", 1, "10.72.72.69", 121));
        }

        [TestMethod]
        public void ExistingConnectionNeedsNoSettingsAndPartialSelectionsAreRefused()
        {
            Portal.ValidateOnlineSelection(null, null, null, null, 30);
            Portal.ValidateOnlineSelection("PN/IE", "NIC", 1, "10.72.72.69", 30);
            Assert.ThrowsException<PortalException>(() => Portal.ValidateOnlineSelection("PN/IE", null, null, null, 30));
            Assert.ThrowsException<PortalException>(() => Portal.ValidateOnlineSelection(null, "NIC", 1, "10.72.72.69", 30));
            Assert.ThrowsException<PortalException>(() => Portal.ValidateOnlineSelection("PN/IE", "NIC", null, "10.72.72.69", 30));
            Assert.ThrowsException<PortalException>(() => Portal.ValidateOnlineSelection("PN/IE", "NIC", 1, null, 30));
            Assert.ThrowsException<PortalException>(() => Portal.ValidateOnlineSelection(null, null, null, null, 0));
        }

        [TestMethod]
        public void ConfiguredTargetInterfaceIsExplicitAndCannotConflictWithAnIp()
        {
            Portal.ValidateOnlineSelection("PN/IE", "NIC", 1, null, 30, "2 X2");
            Assert.ThrowsException<PortalException>(() => Portal.ValidateOnlineSelection("PN/IE", "NIC", 1, "10.72.72.231", 30, "2 X2"));
            Assert.ThrowsException<PortalException>(() => Portal.ValidateOnlineSelection(null, "NIC", 1, null, 30, "2 X2"));
            Assert.ThrowsException<PortalException>(() => Portal.ValidateOnlineSelection("PN/IE", "NIC", null, null, 30, "2 X2"));
            foreach (var invalid in new[] { "", " ", "2 X2\n", new string('X', 129) })
                Assert.ThrowsException<PortalException>(() => Portal.ValidateOnlineSelection("PN/IE", "NIC", 1, null, 30, invalid));
        }

        [TestMethod]
        public void ConnectingIsPolledUntilActualOnlineState()
        {
            var states = new Queue<OnlineState>(new[] { OnlineState.Connecting, OnlineState.Connecting, OnlineState.Online });
            Assert.AreEqual(OnlineState.Online, Portal.WaitForOnlineState(() => states.Dequeue(), OnlineState.Online, TimeSpan.FromSeconds(2)));
            Assert.AreEqual(0, states.Count);
        }

        [TestMethod]
        public void OfflineAndProtectedResultsDoNotBecomeOnline()
        {
            foreach (var terminal in new[] { OnlineState.Offline, OnlineState.Protected, OnlineState.NotReachable, OnlineState.Incompatible })
            {
                var reads = 0;
                var result = Portal.WaitForOnlineState(() => { reads++; return terminal; }, OnlineState.Online, TimeSpan.FromSeconds(2));
                Assert.AreEqual(terminal, result);
                Assert.AreEqual(1, reads);
            }
        }

        [TestMethod]
        public void PendingTransitionRemainsPendingAtDeadline()
        {
            var reads = 0;
            var timer = Stopwatch.StartNew();
            var result = Portal.WaitForOnlineState(() => { reads++; return OnlineState.Connecting; }, OnlineState.Online, TimeSpan.FromMilliseconds(25));
            Assert.AreEqual(OnlineState.Connecting, result);
            Assert.IsTrue(reads >= 2);
            Assert.IsTrue(timer.Elapsed < TimeSpan.FromSeconds(1));
            Assert.AreEqual(OnlineState.Disconnecting, Portal.WaitForOnlineState(() => OnlineState.Disconnecting, OnlineState.Offline, TimeSpan.Zero));
        }

        [TestMethod]
        public void MissingProjectAndInvalidToolInputNeverSendOnlineRequests()
        {
            var previous = McpServer.Portal;
            McpServer.Portal = new Portal();
            try
            {
                Assert.ThrowsException<McpException>(() => McpServer.GetDeviceOnlineState("CPU"));
                Assert.ThrowsException<McpException>(() => McpServer.GetDeviceOnlineState("Station/CPU"));
                Assert.ThrowsException<McpException>(() => McpServer.GoOnlineDevice("Station/CPU", "PN/IE", "NIC", 1, "invalid"));
                Assert.ThrowsException<McpException>(() => McpServer.GoOfflineDevice("Station/CPU", 0));
            }
            finally
            {
                McpServer.Portal = previous;
            }
        }

        [TestMethod]
        public void ConnectionStateIsNeverPresentedAsRuntimeHealth()
        {
            var result = new DeviceOnlineInfo { OnlineServiceAvailable = true, OnlineState = "Online" };
            Assert.IsFalse(result.RuntimeDiagnosticsAvailable);
            StringAssert.Contains(result.RuntimeDiagnosticsExplanation, "not runtime faults");
            Assert.IsNull(result.RequestedStateReached);
            foreach (var name in new[] { "GetDeviceOnlineState", "GetOnlineConnectionOptions" })
            {
                var tool = (McpServerToolAttribute)typeof(McpServer).GetMethod(name)!.GetCustomAttributes(typeof(McpServerToolAttribute), false).Single();
                Assert.IsTrue(tool.ReadOnly);
            }
            foreach (var name in new[] { "GoOnlineDevice", "GoOfflineDevice" })
            {
                var tool = (McpServerToolAttribute)typeof(McpServer).GetMethod(name)!.GetCustomAttributes(typeof(McpServerToolAttribute), false).Single();
                Assert.IsFalse(tool.ReadOnly);
            }
        }

        [TestMethod]
        public void AttributeSerialisationKeepsScalarsAndStopsReferenceCycles()
        {
            Assert.AreEqual(true, ModelContextProtocol.Helper.GetAttributeValue(true));
            Assert.AreEqual(42, ModelContextProtocol.Helper.GetAttributeValue(42));
            Assert.IsNull(ModelContextProtocol.Helper.GetAttributeValue(null));
            Assert.AreEqual("reference", ModelContextProtocol.Helper.GetAttributeValue(new CyclicAttribute()));
        }

        private sealed class CyclicAttribute
        {
            public CyclicAttribute Parent => this;
            public override string ToString() => "reference";
        }
    }
}
