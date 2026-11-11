using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
        private const int MaximumMajorVersion = 19;

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

                var engineeringAssemblyPath = Path.Combine(
                    installPath,
                    "PublicAPI",
                    $"V{majorVersion}",
                    "Siemens.Engineering.dll");
                if (!HasExpectedEngineeringIdentity(
                    engineeringAssemblyPath,
                    majorVersion,
                    out var identityError))
                {
                    invalidEntries.Add($"V{majorVersion}: {identityError}");
                    return;
                }

                installations.Add(new TiaInstallation(majorVersion, installPath));
            }
        }

        private static bool HasExpectedEngineeringIdentity(
            string assemblyPath,
            int majorVersion,
            out string error)
        {
            if (!File.Exists(assemblyPath))
            {
                error =
                    $"expected PublicAPI assembly is missing: {assemblyPath}";
                return false;
            }

            try
            {
                var identity = AssemblyName.GetAssemblyName(assemblyPath);
                var token = identity.GetPublicKeyToken();
                var tokenText = token == null
                    ? string.Empty
                    : BitConverter
                        .ToString(token)
                        .Replace("-", string.Empty)
                        .ToLowerInvariant();
                if (!string.Equals(
                        identity.Name,
                        "Siemens.Engineering",
                        StringComparison.Ordinal) ||
                    !Equals(
                        identity.Version,
                        new Version(majorVersion, 0, 0, 0)) ||
                    !string.Equals(
                        tokenText,
                        "d29ec89bac048f84",
                        StringComparison.Ordinal))
                {
                    error =
                        $"PublicAPI assembly identity is incompatible: " +
                        $"{identity.FullName}";
                    return false;
                }
            }
            catch (Exception exception) when (
                exception is BadImageFormatException ||
                exception is FileLoadException)
            {
                error =
                    $"PublicAPI assembly identity could not be read: " +
                    $"{exception.Message}";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
