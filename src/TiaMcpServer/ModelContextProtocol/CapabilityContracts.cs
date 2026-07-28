using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public sealed class ResponseCapabilities
    {
        public int TiaMajorVersion { get; set; }
        public string? AccessProfile { get; set; }
        public string? SupportStatus { get; set; }
        public string? Contract { get; set; }
        public int DefaultPageLimit { get; set; }
        public int MaximumPageLimit { get; set; }
        public string? DefaultDetailLevel { get; set; }
        public bool CanModifyProject { get; set; }
        public bool CanWriteFiles { get; set; }
        public IEnumerable<string>? ToolFamilies { get; set; }
        public IEnumerable<string>? Guidance { get; set; }
    }
}
