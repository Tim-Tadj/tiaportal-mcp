using System;

namespace TiaMcpServer.Siemens
{
    public static class BuildVersion
    {
#if TIA_MCP_V17
        public const int TiaMajorVersion = 17;
#elif TIA_MCP_V18
        public const int TiaMajorVersion = 18;
#elif TIA_MCP_V19 || TIA_V19
        public const int TiaMajorVersion = 19;
#elif TIA_MCP_V20 || TIA_V20
        public const int TiaMajorVersion = 20;
#elif TIA_MCP_V21 || TIA_V21
        public const int TiaMajorVersion = 21;
#else
#error Build with an exact TiaWorkerVersion from 17 to 21.
#endif

        public static void Validate(int version)
        {
            if (version != TiaMajorVersion)
            {
                throw new ArgumentException($"This executable targets TIA Portal V{TiaMajorVersion}; use the matching build for V{version}.");
            }
        }
    }
}
