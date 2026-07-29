using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;

namespace TiaMcpBroker
{
    internal sealed class TiaInstallation
    {
        public TiaInstallation(int majorVersion, string installPath)
        {
            MajorVersion = majorVersion;
            InstallPath = installPath;
        }

        public int MajorVersion { get; }
        public string InstallPath { get; }
    }

    internal sealed class TiaInstallationScan
    {
        public TiaInstallationScan(
            IReadOnlyList<TiaInstallation> installations,
            IReadOnlyList<string> invalidEntries)
        {
            Installations = installations;
            InvalidEntries = invalidEntries;
        }

        public IReadOnlyList<TiaInstallation> Installations { get; }
        public IReadOnlyList<string> InvalidEntries { get; }
    }

    internal sealed class TiaInstallationDetector
    {
        private const int MinimumMajorVersion = 17;
        private const int MaximumMajorVersion = 20;

        public TiaInstallationScan Scan()
        {
            var installations = new List<TiaInstallation>();
            var invalidEntries = new List<string>();

            try
            {
                using (var localMachine = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    RegistryView.Registry64))
                {
                    for (var majorVersion = MinimumMajorVersion;
                        majorVersion <= MaximumMajorVersion;
                        majorVersion++)
                    {
                        ScanVersion(localMachine, majorVersion, installations, invalidEntries);
                    }
                }
            }
            catch (Exception exception) when (
                exception is SecurityException ||
                exception is UnauthorizedAccessException ||
                exception is PlatformNotSupportedException ||
                exception is IOException)
            {
                throw new BrokerException(
                    BrokerExitCode.InstallationResolutionFailed,
                    "The broker could not inspect the 64-bit Siemens installation registry. " +
                    "Run it on Windows as a user who can read local-machine installation keys.",
                    exception);
            }

            return new TiaInstallationScan(installations, invalidEntries);
        }

        private static void ScanVersion(
            RegistryKey localMachine,
            int majorVersion,
            ICollection<TiaInstallation> installations,
            ICollection<string> invalidEntries)
        {
            var registryPath =
                $@"SOFTWARE\Siemens\Automation\_InstalledSW\TIAP{majorVersion}\TIA_Opns";

            using (var opennessKey = localMachine.OpenSubKey(registryPath))
            {
                if (opennessKey == null)
                {
                    return;
                }

                var rawPath = opennessKey.GetValue("Path")?.ToString();
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    invalidEntries.Add(
                        $"V{majorVersion}: registry key exists but its Path value is empty.");
                    return;
                }

                var installPath = Environment.ExpandEnvironmentVariables(rawPath);
                if (!Directory.Exists(installPath))
                {
                    invalidEntries.Add(
                        $"V{majorVersion}: registry Path does not exist: {installPath}");
                    return;
                }

                installations.Add(new TiaInstallation(majorVersion, installPath));
            }
        }
    }
}
