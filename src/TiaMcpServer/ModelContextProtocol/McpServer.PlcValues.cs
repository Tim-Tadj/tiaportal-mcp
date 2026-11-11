using ModelContextProtocol;
using McpException = ModelContextProtocol.McpProtocolException;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public sealed partial class McpServer
    {
        [McpServerTool(Name = "ReadPlcValues", ReadOnly = true, Idempotent = true), Description("Read live absolute I/Q/M/DB values directly from an explicitly selected PLC over S7 TCP port 102. Does not require a TIA connection, save, download, write, force, reset or start/stop equipment. Optional bounded sampling can observe heartbeat transitions. This is separate from Openness and cannot query a SIMOCODE directly. I/Q are process-image values, not proof of physical contactor or motor state. Reads are not guaranteed to be atomic with the PLC scan. B/W/D values are unsigned. S7-1200/1500 need accessible non-optimised memory and permitted S7 access; no security settings are changed.")]
        public static async Task<PlcValueResult> ReadPlcValues(
            [Description("Explicit PLC IPv4 address, for example 10.72.72.69. Do not use the SIMOCODE IP.")] string targetIpAddress,
            [Description("1..16 uppercase scalar addresses, e.g. I358.4, Q76.5, IB358, DB1000.DBX358.4 or DB1002.DBB76. Symbolic names, timers and peripheral access are unsupported.")] string[] addresses,
            string cpuType = "S7300", int rack = 0, int slot = 2, int sampleCount = 1,
            int intervalMilliseconds = 250, int timeoutMilliseconds = 10000,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await PlcValues.ReadAsync(targetIpAddress, addresses, cpuType, rack, slot, sampleCount, intervalMilliseconds, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
            }
            catch (ArgumentException ex)
            {
                throw new McpException(ex.Message, ex, McpErrorCode.InvalidParams);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new McpException("Live PLC read failed: " + ex.Message, ex, McpErrorCode.InternalError);
            }
        }
    }
}
