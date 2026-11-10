using Siemens.Engineering;
using Siemens.Engineering.Download;
using Siemens.Engineering.HW;
using Siemens.Engineering.Online;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        public DeviceOnlineInfo GetDeviceOnlineState(string deviceItemPath)
        {
            var item = GetOnlineDeviceItem(deviceItemPath);
            var online = item.GetService<OnlineProvider>();
            return new DeviceOnlineInfo
            {
                DeviceItemPath = deviceItemPath,
                Name = item.Name,
                TiaMajorVersion = BuildVersion.TiaMajorVersion,
                OnlineServiceAvailable = online != null,
                DownloadServiceAvailable = item.GetService<DownloadProvider>() != null,
                Services = ((IEngineeringServiceProvider)item).GetServiceInfos().Select(s => s.Type.FullName ?? s.Type.Name).ToArray(),
                OnlineState = online?.State.ToString(),
                Message = online == null
                    ? "This device item does not expose OnlineProvider. Openness cannot go online to it through this service; use its engineering tool's online view. Unsupported is not a device fault."
                    : "Read the native Openness connection state. Online does not establish device health."
            };
        }

        public IEnumerable<OnlineConnectionOption> GetOnlineConnectionOptions(string deviceItemPath)
        {
            var online = RequireOnlineProvider(deviceItemPath);
            return online.Configuration.Modes.SelectMany(mode => mode.PcInterfaces.Select(pc => new OnlineConnectionOption
            {
                Mode = mode.Name,
                PcInterfaceName = pc.Name,
                PcInterfaceNumber = pc.Number
            })).ToArray();
        }

        public DeviceOnlineInfo GoOnlineDevice(string deviceItemPath, string? mode = null, string? pcInterfaceName = null, int? pcInterfaceNumber = null, string? targetIpAddress = null, int timeoutSeconds = 30)
        {
            ValidateOnlineSelection(mode, pcInterfaceName, pcInterfaceNumber, targetIpAddress, timeoutSeconds);
            var online = RequireOnlineProvider(deviceItemPath);
            if (mode != null)
            {
                if (online.State != OnlineState.Offline && online.State != OnlineState.NotReachable)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "The device must be offline before selecting a connection. Read its state and use GoOfflineDevice first. Omit connection options to reuse the existing connection.");
                }
                var configurationMode = online.Configuration.Modes.Find(mode)
                    ?? throw new PortalException(PortalErrorCode.InvalidParams, "Connection mode not found. Use GetOnlineConnectionOptions.");
                var pc = configurationMode.PcInterfaces.Find(pcInterfaceName!, pcInterfaceNumber!.Value)
                    ?? throw new PortalException(PortalErrorCode.InvalidParams, "PC interface not found. Use GetOnlineConnectionOptions and its exact name and number.");
                var address = pc.Addresses.Find(targetIpAddress!) ?? pc.Addresses.Create(targetIpAddress!);
                if (!online.Configuration.ApplyConfiguration(address))
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "TIA rejected the selected online connection configuration. GoOnline was not called. Omit connection options to reuse the existing connection.");
                }
            }
            if (online.State == OnlineState.Online)
            {
                return OnlineResult(deviceItemPath, online.State, OnlineState.Online);
            }
            var timer = Stopwatch.StartNew();
            if (!IsTransition(online.State))
            {
                online.GoOnline();
            }
            var state = WaitForOnlineState(() => online.State, OnlineState.Online, TimeSpan.FromSeconds(timeoutSeconds) - timer.Elapsed);
            return OnlineResult(deviceItemPath, state, OnlineState.Online);
        }

        public DeviceOnlineInfo GoOfflineDevice(string deviceItemPath, int timeoutSeconds = 30)
        {
            ValidateOnlineTimeout(timeoutSeconds);
            var online = RequireOnlineProvider(deviceItemPath);
            var timer = Stopwatch.StartNew();
            online.GoOffline();
            var state = WaitForOnlineState(() => online.State, OnlineState.Offline, TimeSpan.FromSeconds(timeoutSeconds) - timer.Elapsed);
            return OnlineResult(deviceItemPath, state, OnlineState.Offline);
        }

        private DeviceOnlineInfo OnlineResult(string path, OnlineState state, OnlineState requested)
        {
            var result = GetDeviceOnlineState(path);
            result.OnlineState = state.ToString();
            result.RequestedState = requested.ToString();
            result.RequestedStateReached = state == requested;
            result.TimedOut = IsTransition(state);
            result.Message = result.TimedOut
                ? "The connection transition is still pending. The native request may finish later; read GetDeviceOnlineState before deciding on another action."
                : "Native connection request completed. Inspect RequestedStateReached and OnlineState; these do not report runtime health.";
            return result;
        }

        private OnlineProvider RequireOnlineProvider(string path)
        {
            return GetOnlineDeviceItem(path).GetService<OnlineProvider>()
                ?? throw new PortalException(PortalErrorCode.InvalidState, "This device item does not expose Siemens OnlineProvider. Use its engineering tool's online diagnostics/commissioning view; no online request was sent.");
        }

        private DeviceItem GetOnlineDeviceItem(string path)
        {
            ValidateOnlineDevicePath(path);
            if (_project == null)
            {
                throw new PortalException(PortalErrorCode.InvalidState, "No project is open in the attached TIA Portal instance.");
            }
            var targets = DeviceItems(_project.Devices, "")
                .Concat(DeviceItems(_project.UngroupedDevicesGroup.Devices, "UngroupedDevices/"))
                .Concat(GroupDeviceItems(_project.DeviceGroups, ""))
                .Where(pair => string.Equals(pair.Key, path, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (targets.Length != 1)
            {
                throw new PortalException(PortalErrorCode.NotFound, $"Expected exactly one device item at '{path}', found {targets.Length}. Use its full group/device/item path, including nested items.");
            }
            return targets[0].Value;
        }

        private static IEnumerable<KeyValuePair<string, DeviceItem>> GroupDeviceItems(DeviceUserGroupComposition groups, string prefix)
        {
            foreach (var group in groups)
            {
                var path = prefix + group.Name + "/";
                foreach (var item in DeviceItems(group.Devices, path).Concat(GroupDeviceItems(group.Groups, path)))
                {
                    yield return item;
                }
            }
        }

        private static IEnumerable<KeyValuePair<string, DeviceItem>> DeviceItems(DeviceComposition devices, string prefix)
        {
            foreach (var device in devices)
            {
                foreach (var item in NestedDeviceItems(device.DeviceItems, prefix + device.Name + "/"))
                {
                    yield return item;
                }
            }
        }

        private static IEnumerable<KeyValuePair<string, DeviceItem>> NestedDeviceItems(DeviceItemComposition items, string prefix)
        {
            foreach (var item in items)
            {
                var path = prefix + item.Name;
                yield return new KeyValuePair<string, DeviceItem>(path, item);
                foreach (var child in NestedDeviceItems(item.DeviceItems, path + "/"))
                {
                    yield return child;
                }
            }
        }

        internal static void ValidateOnlineDevicePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.Contains('/') || path.Split('/').Any(part => string.IsNullOrWhiteSpace(part) || part == "." || part == ".."))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "Use a full group/device/item path with no empty, '.' or '..' segments. Bare names are not accepted for online operations.");
            }
        }

        internal static void ValidateOnlineConnection(string mode, string pcName, int pcNumber, string ip, int timeoutSeconds)
        {
            ValidateOnlineTimeout(timeoutSeconds);
            if (string.IsNullOrWhiteSpace(mode) || string.IsNullOrWhiteSpace(pcName) || pcNumber < 0
                || !IPAddress.TryParse(ip, out var address) || address.AddressFamily != AddressFamily.InterNetwork
                || address.ToString() != ip || ip == "0.0.0.0" || ip == "255.255.255.255")
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "Use a mode, exact PC interface name and number, and a full dotted IPv4 target address. No online request was sent.");
            }
        }

        internal static void ValidateOnlineSelection(string? mode, string? pcName, int? pcNumber, string? ip, int timeoutSeconds)
        {
            ValidateOnlineTimeout(timeoutSeconds);
            if (mode == null && pcName == null && pcNumber == null && ip == null)
            {
                return;
            }
            if (mode == null || pcName == null || pcNumber == null || ip == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "Supply all four connection options, or omit all four to reuse TIA's existing connection. No online request was sent.");
            }
            ValidateOnlineConnection(mode, pcName, pcNumber.Value, ip, timeoutSeconds);
        }

        private static void ValidateOnlineTimeout(int seconds)
        {
            if (seconds < 1 || seconds > 120)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "timeoutSeconds must be between 1 and 120.");
            }
        }

        internal static OnlineState WaitForOnlineState(Func<OnlineState> read, OnlineState desired, TimeSpan remaining)
        {
            var timer = Stopwatch.StartNew();
            var state = read();
            while (state != desired && IsTransition(state) && timer.Elapsed < remaining)
            {
                Thread.Sleep((int)Math.Min(100, Math.Max(1, (remaining - timer.Elapsed).TotalMilliseconds)));
                state = read();
            }
            return state;
        }

        private static bool IsTransition(OnlineState state) => state == OnlineState.Connecting || state == OnlineState.Disconnecting;
    }
}
