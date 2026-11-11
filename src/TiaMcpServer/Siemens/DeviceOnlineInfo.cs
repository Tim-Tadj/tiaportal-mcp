using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    public class DeviceOnlineInfo
    {
        public string? DeviceItemPath { get; set; }
        public string? Name { get; set; }
        public int TiaMajorVersion { get; set; }
        public bool OnlineServiceAvailable { get; set; }
        public bool DownloadServiceAvailable { get; set; }
        public IEnumerable<string>? Services { get; set; }
        public string? OnlineState { get; set; }
        public string? RequestedState { get; set; }
        public bool? RequestedStateReached { get; set; }
        public bool TimedOut { get; set; }
        public string? Message { get; set; }
        public bool RuntimeDiagnosticsAvailable => false;
        public string RuntimeDiagnosticsExplanation => "OnlineProvider reports the engineering connection state, not runtime faults, warnings, motor state or diagnostic-buffer entries. Use the device's online diagnostics/commissioning view for health checks.";
    }

    public class OnlineConnectionOption
    {
        public string? Mode { get; set; }
        public string? PcInterfaceName { get; set; }
        public int PcInterfaceNumber { get; set; }
    }
}
