using System;

namespace TiaMcpServer.Siemens
{
    public static class BuildVersion
    {
#if TIA_V19
        public const int TiaMajorVersion = 19;
#elif TIA_V20
        public const int TiaMajorVersion = 20;
#elif TIA_V21
        public const int TiaMajorVersion = 21;
#else
#error Build with TiaMajorVersion=19, 20 or 21.
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
