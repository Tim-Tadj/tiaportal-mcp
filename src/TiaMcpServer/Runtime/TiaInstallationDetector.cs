using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;

namespace TiaMcpServer.Runtime
{
    public interface ITiaInstallationDetector
    {
        TiaInstallationScan Scan();
    }

    public sealed class TiaInstallationDetector : ITiaInstallationDetector
    {
        private const int MinimumSupportedMajorVersion = 17;
        private const int MaximumSupportedMajorVersion = 21;

        public TiaInstallationScan Scan()
        {
            var installations = new List<TiaInstallation>();
            var invalidInstallations = new List<string>();

            try
            {
                using (var localMachine = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    RegistryView.Registry64))
                {
                    for (var majorVersion = MinimumSupportedMajorVersion;
                        majorVersion <= MaximumSupportedMajorVersion;
                        majorVersion++)
                    {
                        ScanVersion(localMachine, majorVersion, installations, invalidInstallations);
                    }
                }
            }
            catch (Exception exception) when (
                exception is SecurityException ||
                exception is UnauthorizedAccessException ||
                exception is PlatformNotSupportedException)
            {
                throw new RuntimeSelectionException(
                    "TIA Portal installations could not be inspected in the 64-bit local-machine registry. " +
                    "Run this Windows executable as a user who can read the Siemens installation keys.",
                    exception);
            }

            return new TiaInstallationScan(installations, invalidInstallations);
        }

        private static void ScanVersion(
            RegistryKey localMachine,
            int majorVersion,
            ICollection<TiaInstallation> installations,
            ICollection<string> invalidInstallations)
        {
            var subKeyName =
                $@"SOFTWARE\Siemens\Automation\_InstalledSW\TIAP{majorVersion}\TIA_Opns";

            using (var opennessKey = localMachine.OpenSubKey(subKeyName))
            {
                if (opennessKey == null)
                {
                    return;
                }

                var rawPath = opennessKey.GetValue("Path")?.ToString();
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    invalidInstallations.Add(
                        $"V{majorVersion}: registry key exists but its Path value is empty.");
                    return;
                }

                var installPath = Environment.ExpandEnvironmentVariables(rawPath);
                if (!Directory.Exists(installPath))
                {
                    invalidInstallations.Add(
                        $"V{majorVersion}: registry Path does not exist: {installPath}");
                    return;
                }

                installations.Add(new TiaInstallation(majorVersion, installPath));
            }
        }
    }
}
