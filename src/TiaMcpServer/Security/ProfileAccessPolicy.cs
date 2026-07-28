using System;

namespace TiaMcpServer.Security
{
    public static class ProfileAccessPolicy
    {
        public static void DemandWrite(string operation)
        {
#if TIA_MCP_READ_WRITE
            return;
#else
            throw new InvalidOperationException(
                $"The read-only worker rejected the mutating operation '{operation}'.");
#endif
        }
    }
}
