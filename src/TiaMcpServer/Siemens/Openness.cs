using System;
using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
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

        internal static string GetWorkerTrust(int version)
        {
            if (version != 19) return "NotChecked";
            try
            {
                var worker = new FileInfo(Assembly.GetExecutingAssembly().Location);
                using var registry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var approvals = registry.OpenSubKey(@"SOFTWARE\Siemens\Automation\Openness\19.0\Whitelist\" + worker.Name);
                if (approvals == null) return "NoMatchingEntry";

                using var sha256 = SHA256.Create();
                using var stream = worker.OpenRead();
                var fileHash = Convert.ToBase64String(sha256.ComputeHash(stream));
                var modified = worker.LastWriteTimeUtc.ToString("yyyy/MM/dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                foreach (var keyName in approvals.GetSubKeyNames())
                {
                    using var approval = approvals.OpenSubKey(keyName);
                    if (string.Equals(approval?.GetValue("Path") as string, worker.FullName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(approval?.GetValue("FileHash") as string, fileHash, StringComparison.Ordinal) &&
                        string.Equals(approval?.GetValue("DateModified") as string, modified, StringComparison.Ordinal))
                        return "Matched";
                }
                return "NoMatchingEntry";
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException)
            {
                return "Unavailable";
            }
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
