using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Server;
using S7.Net;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TiaMcpServer.ModelContextProtocol;
using McpServer = TiaMcpServer.ModelContextProtocol.McpServer;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    public class TestPlcValues
    {
        private static void Validate(string[] addresses, string ip = "10.72.72.69", string cpu = "S7300", int rack = 0, int slot = 2, int count = 1, int interval = 250, int timeout = 10000)
        {
            PlcValues.Validate(ip, addresses, cpu, rack, slot, count, interval, timeout);
        }

        [TestMethod]
        public void BenchAddressesParseToTheExpectedAreasAndBits()
        {
            var items = PlcValues.Validate("10.72.72.69", new[] { "I358.4", "Q76.5", "DB1000.DBX358.4", "DB1002.DBB76", "IW360", "MD100" }, "S7300", 0, 2, 1, 250, 10000);
            Assert.AreEqual(DataType.Input, items[0].DataType);
            Assert.AreEqual(358, items[0].StartByteAdr);
            Assert.AreEqual((byte)4, items[0].BitAdr);
            Assert.AreEqual(DataType.Output, items[1].DataType);
            Assert.AreEqual((byte)5, items[1].BitAdr);
            Assert.AreEqual(1000, items[2].DB);
            Assert.AreEqual(VarType.Byte, items[3].VarType);
            Assert.AreEqual(VarType.Word, items[4].VarType);
            Assert.AreEqual(VarType.DWord, items[5].VarType);
        }

        [TestMethod]
        public void RejectsAmbiguousUnsupportedOrOutOfRangeAddresses()
        {
            foreach (var address in new[] { "", "i358.4", "%I358.4", "I358", "I358.8", "DB0.DBX0.0", "DB1.DBX0", "DB1.DBW0.extra", "DB65536.DBX0.0", "IB65536", "IW65535", "ID65533", "DB1.DBD65533", "T1", "Q0.0:P", "Symbol.Tag", "IB-1", " IB358", "IB358\n" })
            {
                Assert.ThrowsException<ArgumentException>(() => Validate(new[] { address }), address);
            }
            Validate(new[] { "IB65535", "IW65534", "ID65532", "DB65535.DBX65535.7" });
        }

        [TestMethod]
        public void RejectsInvalidTargetsBeforeAnyNetworkRequest()
        {
            foreach (var ip in new[] { "hostname", "10.69", "010.072.072.069", "0.0.0.0", "224.0.0.1", "255.255.255.255", "::1", "10.72.72.999" })
            {
                Assert.ThrowsException<ArgumentException>(() => Validate(new[] { "I358.4" }, ip: ip), ip);
            }
            Assert.ThrowsException<ArgumentException>(() => Validate(new[] { "I358.4" }, cpu: "S7300-invalid"));
            Assert.ThrowsException<ArgumentException>(() => Validate(new[] { "I358.4" }, rack: 8));
            Assert.ThrowsException<ArgumentException>(() => Validate(new[] { "I358.4" }, slot: -1));
        }

        [TestMethod]
        public void BoundsPayloadAndSamplingBeforeAnyNetworkRequest()
        {
            Assert.ThrowsException<ArgumentException>(() => Validate(null!));
            Assert.ThrowsException<ArgumentException>(() => Validate(Array.Empty<string>()));
            Assert.ThrowsException<ArgumentException>(() => Validate(Enumerable.Repeat("ID0", 17).ToArray()));
            Assert.ThrowsException<ArgumentException>(() => Validate(new[] { "I0.0" }, count: 121));
            Assert.ThrowsException<ArgumentException>(() => Validate(new[] { "I0.0" }, interval: 49));
            Assert.ThrowsException<ArgumentException>(() => Validate(new[] { "I0.0" }, timeout: 60001));
            Assert.ThrowsException<ArgumentException>(() => Validate(new[] { "I0.0" }, count: 5, interval: 250, timeout: 1000));
            Validate(Enumerable.Repeat("ID0", 16).ToArray(), count: 40, timeout: 20000);
        }

        [TestMethod]
        public void RuntimeReaderIsAnnotatedReadOnly()
        {
            var method = typeof(McpServer).GetMethod(nameof(McpServer.ReadPlcValues))!;
            var attribute = (McpServerToolAttribute)method.GetCustomAttributes(typeof(McpServerToolAttribute), false).Single();
            Assert.IsTrue(attribute.ReadOnly);
        }

        [TestMethod]
        public async Task DeadlineReturnsEvenIfTheDriverOperationCannotBeCancelled()
        {
            var pending = new TaskCompletionSource<bool>();
            using (var cancellation = new CancellationTokenSource())
            {
                var waiting = PlcValues.WaitForReadAsync(pending.Task, cancellation.Token);
                cancellation.Cancel();
                Assert.AreSame(waiting, await Task.WhenAny(waiting, Task.Delay(2000)));
                await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => waiting);
                Assert.IsFalse(pending.Task.IsCompleted);
                pending.SetResult(true);
            }
        }

        [TestMethod]
        public async Task FailedOrCancelledReadsAreNeverReturnedAsSuccessfulSamples()
        {
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => PlcValues.WaitForReadAsync(Task.FromException(new InvalidOperationException("Read failed")), CancellationToken.None));
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => PlcValues.WaitForReadAsync(Task.CompletedTask, cancellation.Token));
            }
        }
    }
}
