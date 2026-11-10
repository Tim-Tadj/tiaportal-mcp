using S7.Net;
using S7.Net.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TiaMcpServer.Siemens
{
    public sealed class PlcValue
    {
        public string Address { get; set; } = "";
        public string DataType { get; set; } = "";
        public object Value { get; set; } = false;
    }

    public sealed class PlcValueSample
    {
        public DateTimeOffset StartedAtUtc { get; set; }
        public DateTimeOffset CompletedAtUtc { get; set; }
        public List<PlcValue> Values { get; set; } = new List<PlcValue>();
    }

    public sealed class PlcValueResult
    {
        public string TargetIpAddress { get; set; } = "";
        public string CpuType { get; set; } = "";
        public int Rack { get; set; }
        public int Slot { get; set; }
        public string Transport { get; set; } = "Direct S7 TCP port 102, independent of the TIA engineering session";
        public string Interpretation { get; set; } = "I and Q are PLC process-image values. Q does not prove physical output or contactor state. Samples are not guaranteed to be atomic with the PLC scan. B/W/D values are unsigned; no motor commands or writes are issued.";
        public List<PlcValueSample> Samples { get; set; } = new List<PlcValueSample>();
    }

    public static class PlcValues
    {
        private static readonly SemaphoreSlim ReadGate = new SemaphoreSlim(1, 1);
        private static readonly Regex AddressPattern = new Regex(@"\A(?:[IQM](?:[BWD][0-9]{1,5}|[0-9]{1,5}\.[0-7])|DB[1-9][0-9]{0,4}\.DB(?:[BWD][0-9]{1,5}|X[0-9]{1,5}\.[0-7]))\z", RegexOptions.CultureInvariant);

        internal static List<DataItem> Validate(string targetIpAddress, string[] addresses, string cpuType, int rack, int slot, int sampleCount, int intervalMilliseconds, int timeoutMilliseconds)
        {
            if (!IPAddress.TryParse(targetIpAddress, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork || ip.ToString() != targetIpAddress || ip.GetAddressBytes()[0] == 0 || ip.GetAddressBytes()[0] >= 224)
            {
                throw new ArgumentException("Use a complete unicast IPv4 address for the explicitly selected PLC.");
            }
            if (!new[] { "S7300", "S7400", "S71200", "S71500" }.Contains(cpuType))
            {
                throw new ArgumentException("cpuType must be S7300, S7400, S71200 or S71500. S7-1200/1500 require accessible non-optimised memory and permitted S7 access.");
            }
            if (rack < 0 || rack > 7 || slot < 0 || slot > 31)
            {
                throw new ArgumentException("Rack must be 0..7 and slot 0..31. The S7-300 CPU is normally rack 0, slot 2.");
            }
            if (sampleCount < 1 || sampleCount > 120 || intervalMilliseconds < 50 || intervalMilliseconds > 5000 || timeoutMilliseconds < 1000 || timeoutMilliseconds > 60000 || (sampleCount - 1) * intervalMilliseconds >= timeoutMilliseconds)
            {
                throw new ArgumentException("Use 1..120 samples, 50..5000 ms interval and 1000..60000 ms total timeout. Sampling delays must fit within the timeout, with time left for network reads.");
            }
            // Sixteen scalar items fit the minimum 240-byte S7 PDU, including DWORDs.
            if (addresses == null || addresses.Length < 1 || addresses.Length > 16)
            {
                throw new ArgumentException("Supply 1..16 absolute scalar addresses, for example I358.4, Q76.5, IB358 or DB1000.DBX358.4.");
            }
            var items = new List<DataItem>();
            foreach (var address in addresses)
            {
                if (address == null || !AddressPattern.IsMatch(address))
                {
                    throw new ArgumentException("Unsupported address: " + address + ". Use uppercase I/Q/M bits, bytes, words or DWORDs, or DBn.DBX/DBB/DBW/DBD. Symbolic names and peripheral access are not supported.");
                }
                var item = DataItem.FromAddress(address);
                var width = item.VarType == VarType.DWord ? 4 : item.VarType == VarType.Word ? 2 : 1;
                if (item.DB > 65535 || item.StartByteAdr > 65536 - width)
                {
                    throw new ArgumentException("Address exceeds the supported DB or byte range: " + address);
                }
                items.Add(item);
            }
            return items;
        }

        public static async Task<PlcValueResult> ReadAsync(string targetIpAddress, string[] addresses, string cpuType, int rack, int slot, int sampleCount, int intervalMilliseconds, int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            var items = Validate(targetIpAddress, addresses, cpuType, rack, slot, sampleCount, intervalMilliseconds, timeoutMilliseconds);
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(timeoutMilliseconds);
                try
                {
                    await ReadGate.WaitAsync(deadline.Token).ConfigureAwait(false);
                    var operation = ReadConnectedAsync(targetIpAddress, addresses, items, cpuType, rack, slot, sampleCount, intervalMilliseconds, timeoutMilliseconds, deadline.Token);
                    // A pending net48 TCP connect cannot be aborted through the driver's API.
                    // Keep its resources until completion and permit only one such operation.
                    _ = operation.ContinueWith(completed =>
                    {
                        _ = completed.Exception;
                        ReadGate.Release();
                    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    await WaitForReadAsync(operation, deadline.Token).ConfigureAwait(false);
                    return await operation.ConfigureAwait(false);
                }
                catch (Exception ex) when (deadline.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException("PLC read exceeded its total deadline. A pending Windows TCP connect may still be cleaning up; later reads wait for it. No complete result is returned; do not interpret missing samples as healthy or zero.", ex);
                }
            }
        }

        internal static async Task WaitForReadAsync(Task operation, CancellationToken cancellationToken)
        {
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(operation, cancelled.Task).ConfigureAwait(false) != operation)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
                await operation.ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private static async Task<PlcValueResult> ReadConnectedAsync(string targetIpAddress, string[] addresses, List<DataItem> items, string cpuType, int rack, int slot, int sampleCount, int intervalMilliseconds, int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            var result = new PlcValueResult { TargetIpAddress = targetIpAddress, CpuType = cpuType, Rack = rack, Slot = slot };
            using (var plc = new Plc((CpuType)Enum.Parse(typeof(CpuType), cpuType), targetIpAddress, (short)rack, (short)slot))
            {
                plc.ReadTimeout = timeoutMilliseconds;
                plc.WriteTimeout = timeoutMilliseconds;
                using (cancellationToken.Register(() => { if (plc.IsConnected) plc.Close(); }))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await plc.OpenAsync(cancellationToken).ConfigureAwait(false);
                    for (var i = 0; i < sampleCount; i++)
                    {
                        if (i > 0)
                        {
                            await Task.Delay(intervalMilliseconds, cancellationToken).ConfigureAwait(false);
                        }
                        var sample = new PlcValueSample { StartedAtUtc = DateTimeOffset.UtcNow };
                        await plc.ReadMultipleVarsAsync(items, cancellationToken).ConfigureAwait(false);
                        sample.CompletedAtUtc = DateTimeOffset.UtcNow;
                        for (var j = 0; j < items.Count; j++)
                        {
                            var value = items[j].Value ?? throw new InvalidOperationException("No value returned for " + addresses[j]);
                            sample.Values.Add(new PlcValue { Address = addresses[j], DataType = items[j].VarType.ToString(), Value = value });
                        }
                        result.Samples.Add(sample);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    return result;
                }
            }
        }
    }
}
