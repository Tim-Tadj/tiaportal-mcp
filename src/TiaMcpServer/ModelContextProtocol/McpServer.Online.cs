using ModelContextProtocol;
using McpException = ModelContextProtocol.McpProtocolException;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public sealed partial class McpServer
    {
        [McpServerTool(Name = "GetDeviceOnlineState", ReadOnly = true, Idempotent = true), Description("Read native Openness online state and service availability for an exact device-item path. Unsupported is not a device fault. This does not read runtime faults, warnings, motor state or diagnostic buffers, and does not save the project.")]
        public static DeviceOnlineInfo GetDeviceOnlineState([Description("Full group/device/item path. For example Gatton_STP01/Gatton_STP01. Bare names are refused.")] string deviceItemPath)
        {
            return OnlineCall(() => Portal.GetDeviceOnlineState(deviceItemPath));
        }

        [McpServerTool(Name = "GetOnlineConnectionOptions", ReadOnly = true, Idempotent = true), Description("List native connection modes and PC interface names/numbers for a device exposing OnlineProvider. Does not change connection settings or save the project.")]
        public static IEnumerable<OnlineConnectionOption> GetOnlineConnectionOptions(string deviceItemPath)
        {
            return OnlineCall(() => Portal.GetOnlineConnectionOptions(deviceItemPath));
        }

#if TIA_MCP_READ_WRITE
        [McpServerTool(Name = "GoOnlineDevice", ReadOnly = false, Destructive = false), Description("Request native GoOnline, reusing TIA's existing connection by default. To change connection settings, supply all four connection options; TIA may reject configuration changes while another session is online. Does not download, start/stop the CPU, control a motor or save the project. Timeout cannot interrupt the synchronous Siemens call. Read state before retrying a pending request.")]
        public static DeviceOnlineInfo GoOnlineDevice(string deviceItemPath, string? mode = null, string? pcInterfaceName = null, int? pcInterfaceNumber = null, string? targetIpAddress = null, int timeoutSeconds = 30)
        {
            return OnlineCall(() => Portal.GoOnlineDevice(deviceItemPath, mode, pcInterfaceName, pcInterfaceNumber, targetIpAddress, timeoutSeconds));
        }

        [McpServerTool(Name = "GoOfflineDevice", ReadOnly = false, Destructive = false), Description("Disconnect the native engineering online session for a supported device. Does not stop the CPU, remove fieldbus communications, control a motor or save the project. Read state before retrying a pending request.")]
        public static DeviceOnlineInfo GoOfflineDevice(string deviceItemPath, int timeoutSeconds = 30)
        {
            return OnlineCall(() => Portal.GoOfflineDevice(deviceItemPath, timeoutSeconds));
        }

#endif

        private static T OnlineCall<T>(Func<T> action)
        {
            try
            {
                return action();
            }
            catch (PortalException ex)
            {
                throw new McpException(ex.Message, ex, McpErrorCode.InvalidParams);
            }
            catch (Exception ex)
            {
                throw new McpException("Native online request failed: " + ex.Message, ex, McpErrorCode.InternalError);
            }
        }
    }
}
