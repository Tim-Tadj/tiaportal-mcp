using System;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using TiaMcpServer.Runtime;

namespace TiaMcpServer.Siemens
{
    public static class Openness
    {
        private const string TiaOpennessGroupName = "Siemens TIA Openness";

        public static void Initialize(
            int? tiaMajorVersion = null,
            string? tiaInstallPath = null)
        {
            var selectedVersion =
                tiaMajorVersion ?? WorkerBuild.Current.TiaMajorVersion;
            if (string.IsNullOrWhiteSpace(tiaInstallPath))
            {
                var installation = new TiaInstallationDetector()
                    .Scan()
                    .Installations
                    .SingleOrDefault(candidate =>
                        candidate.MajorVersion == selectedVersion);
                if (installation == null)
                {
                    throw new InvalidOperationException(
                        $"Could not find a valid TIA Portal V{selectedVersion} " +
                        "Openness installation.");
                }

                tiaInstallPath = installation.InstallPath;
            }

            if (tiaInstallPath == null)
            {
                throw new InvalidOperationException(
                    "The TIA Portal installation path was not resolved.");
            }

            Engineering.Configure(selectedVersion, tiaInstallPath);
            AppDomain.CurrentDomain.AssemblyResolve -= Engineering.Resolver;
            AppDomain.CurrentDomain.AssemblyResolve += Engineering.Resolver;
            Engineering.Preflight();
        }

        public static Task<bool> IsUserInGroup()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var groups = identity.Groups;
            if (groups == null)
            {
                return Task.FromResult(false);
            }

            SecurityIdentifier localGroupSid;
            try
            {
                localGroupSid = (SecurityIdentifier)new NTAccount(
                    Environment.MachineName,
                    TiaOpennessGroupName).Translate(
                        typeof(SecurityIdentifier));
            }
            catch (IdentityNotMappedException)
            {
                return Task.FromResult(false);
            }

            var isMember = new WindowsPrincipal(identity).IsInRole(
                localGroupSid);

            return Task.FromResult(isMember);
        }
    }
}
