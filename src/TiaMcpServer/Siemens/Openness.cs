using System;
using System.Security.Principal;
using System.Threading.Tasks;

namespace TiaMcpServer.Siemens
{
    public static class Openness
    {
        public static int TiaMajorVersion { get; private set; }

        public static void Initialize(int? tiaMajorVersion = null)
        {
            var version = tiaMajorVersion ?? BuildVersion.TiaMajorVersion;
            BuildVersion.Validate(version);
            TiaMajorVersion = version;
            Engineering.TiaMajorVersion = version;
            AppDomain.CurrentDomain.AssemblyResolve -= Engineering.Resolver;
            AppDomain.CurrentDomain.AssemblyResolve += Engineering.Resolver;
        }

        public static Task<bool> IsUserInGroup()
        {
            var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            return Task.FromResult(principal.IsInRole("Siemens TIA Openness"));
        }
    }
}
