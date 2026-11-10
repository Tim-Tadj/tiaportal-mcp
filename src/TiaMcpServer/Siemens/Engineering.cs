using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TiaMcpServer.Siemens
{
    // Manual Siemens.Engineering.dll resolve
    public class Engineering
    {
        public static int TiaMajorVersion { get; set; }

        public static Assembly? Resolver(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);
            if (!assemblyName.Name.StartsWith("Siemens.Engineering"))
            {
                return null;
            }

            var tiaInstallPath = GetTiaPortalInstallPath();
            if (string.IsNullOrEmpty(tiaInstallPath))
            {
                throw new InvalidOperationException($"Could not find TIA Portal installation path for version {TiaMajorVersion} in the registry.");
            }

            var tiaMajorVersionString = TiaMajorVersion.ToString();
            var searchDirectories = GetApiSearchDirectories(tiaInstallPath!, TiaMajorVersion);

            // IEnumerable without given majorVersionString
            var excludedTiaMajorVersions = Enumerable.Range(13, 9).Select(v => $"V{v}")
                                    .Where(v => v != $"V{tiaMajorVersionString}");

            foreach (var dir in searchDirectories)
            {
                var assemblyPath = FindAssemblyRecursive(dir, assemblyName.Name + ".dll", excludedTiaMajorVersions);
                if (assemblyPath != null)
                {
                    if (AssemblyName.GetAssemblyName(assemblyPath).Version.Major != TiaMajorVersion)
                    {
                        throw new InvalidOperationException($"The Siemens API at '{assemblyPath}' does not match TIA Portal V{TiaMajorVersion}.");
                    }
                    return Assembly.LoadFrom(assemblyPath);
                }
            }

            throw new FileNotFoundException($"Could not find DLL '{assemblyName.Name}' for TIA Portal version {TiaMajorVersion} in the installation directories.");
        }

        internal static string[] GetApiSearchDirectories(string installPath, int majorVersion)
        {
            var apiPath = Path.Combine(installPath, "PublicAPI", $"V{majorVersion}");
            // V21 separates .NET Framework and modern .NET API assemblies.
            return majorVersion >= 21
                ? new[] { Path.Combine(apiPath, "net48") }
                : new[] { apiPath, Path.Combine(installPath, "Bin", "PublicAPI") };
        }

        private static string? GetTiaPortalInstallPath()
        {
            var configuredPath = Environment.GetEnvironmentVariable("TiaPortalLocation");
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return configuredPath;
            }

            var subKeyName = $@"SOFTWARE\Siemens\Automation\_InstalledSW\TIAP{TiaMajorVersion}\TIA_Opns";

            using (var regBaseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var tiaOpnsKey = regBaseKey.OpenSubKey(subKeyName))
            {
                return tiaOpnsKey?.GetValue("Path")?.ToString();
            }
        }

        private static string? FindAssemblyRecursive(string directory, string fileName, IEnumerable<string> excludedTiaMajorVersions)
        {
            if (!Directory.Exists(directory))
            {
                return null;
            }

            var filePath = Path.Combine(directory, fileName);
            if (File.Exists(filePath))
            {
                return filePath;
            }

            foreach (var subDir in Directory.GetDirectories(directory))
            {
                var subDirName = new DirectoryInfo(subDir).Name;
                if (excludedTiaMajorVersions.Contains(subDirName))
                {
                    continue;
                }

                var result = FindAssemblyRecursive(subDir, fileName, excludedTiaMajorVersions);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
