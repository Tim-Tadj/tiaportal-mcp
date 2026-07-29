using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TiaMcpServer.Runtime;

namespace TiaMcpServer.Siemens
{
    // Resolves Siemens Engineering assemblies from the selected local TIA Portal installation.
    public class Engineering
    {
        private static string? tiaInstallPath;

        public static int TiaMajorVersion { get; private set; }

        public static void Configure(int tiaMajorVersion, string installPath)
        {
            if (tiaMajorVersion < 17 || tiaMajorVersion > 21)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tiaMajorVersion),
                    "TIA Portal major version must be between 17 and 21.");
            }

            if (string.IsNullOrWhiteSpace(installPath))
            {
                throw new ArgumentException(
                    "The TIA Portal installation path is required.",
                    nameof(installPath));
            }

            var expandedPath = Environment.ExpandEnvironmentVariables(
                installPath);
            var fullPath = Path.GetFullPath(expandedPath);
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException(
                    $"The selected TIA Portal installation path does not exist: '{fullPath}'.");
            }

            TiaMajorVersion = tiaMajorVersion;
            tiaInstallPath = fullPath;
        }

        public static void Preflight()
        {
            GetConfiguredInstallPath();

            var pending = new Queue<AssemblyName>();
            pending.Enqueue(new AssemblyName(
                $"Siemens.Engineering, Version={TiaMajorVersion}.0.0.0, " +
                "Culture=neutral, PublicKeyToken=d29ec89bac048f84"));

            foreach (var reference in Assembly
                .GetExecutingAssembly()
                .GetReferencedAssemblies()
                .Where(reference =>
                    SiemensEngineeringAssemblyPolicy.IsSupportedName(
                        reference.Name)))
            {
                pending.Enqueue(reference);
            }

            var loadedIdentities = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            while (pending.Count > 0)
            {
                var requested = pending.Dequeue();
                if (!loadedIdentities.Add(
                    requested.FullName ?? requested.Name ?? string.Empty))
                {
                    continue;
                }

                SiemensEngineeringAssemblyPolicy.DemandTrustedRequest(
                    requested);
                var loaded = Assembly.Load(requested);
                foreach (var reference in loaded
                    .GetReferencedAssemblies()
                    .Where(reference =>
                        SiemensEngineeringAssemblyPolicy.IsSupportedName(
                            reference.Name)))
                {
                    pending.Enqueue(reference);
                }
            }
        }

        public static Assembly? Resolver(object sender, ResolveEventArgs args)
        {
            var requested = new AssemblyName(args.Name);
            if (!SiemensEngineeringAssemblyPolicy.IsSupportedName(
                requested.Name))
            {
                return null;
            }

            SiemensEngineeringAssemblyPolicy.DemandTrustedRequest(requested);

            var installPath = GetConfiguredInstallPath();
            var versionDirectory = $"V{TiaMajorVersion}";
            var searchDirectories = new[]
            {
                Path.Combine(installPath, "PublicAPI", versionDirectory),
                Path.Combine(installPath, "Bin", "PublicAPI")
            };
            var excludedTiaVersions = new[]
            {
                "V13",
                "V14",
                "V15",
                "V16",
                "V17",
                "V18",
                "V19",
                "V20",
                "V21"
            }.Where(version => version != versionDirectory);

            var candidates = searchDirectories
                .SelectMany(directory => FindAssembliesRecursive(
                    directory,
                    requested.Name + ".dll",
                    excludedTiaVersions))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            var assemblyPath =
                SiemensEngineeringAssemblyPolicy.FindFirstMatch(
                    requested,
                    candidates,
                    TryGetAssemblyName);
            if (assemblyPath == null)
            {
                throw new FileNotFoundException(
                    $"Could not find an exact signed match for '{requested.FullName}' " +
                    $"in the selected TIA Portal V{TiaMajorVersion} installation.");
            }

            return Assembly.LoadFrom(assemblyPath);
        }

        private static string GetConfiguredInstallPath()
        {
            return tiaInstallPath
                ?? throw new InvalidOperationException(
                    "The Siemens Engineering resolver has not been configured " +
                    "with a locked TIA Portal installation path.");
        }

        private static AssemblyName? TryGetAssemblyName(string assemblyPath)
        {
            try
            {
                return AssemblyName.GetAssemblyName(assemblyPath);
            }
            catch (Exception exception) when (
                exception is BadImageFormatException ||
                exception is FileLoadException ||
                exception is FileNotFoundException)
            {
                return null;
            }
        }

        private static IEnumerable<string> FindAssembliesRecursive(
            string directory,
            string fileName,
            IEnumerable<string> excludedTiaVersions)
        {
            if (!Directory.Exists(directory))
            {
                yield break;
            }

            var filePath = Path.Combine(directory, fileName);
            if (File.Exists(filePath))
            {
                yield return filePath;
            }

            foreach (var subDirectory in Directory
                .GetDirectories(directory)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var subDirectoryName = Path.GetFileName(subDirectory);
                if (excludedTiaVersions.Any(version =>
                    subDirectoryName.Equals(
                        version,
                        StringComparison.OrdinalIgnoreCase) ||
                    subDirectoryName.StartsWith(
                        version + ".",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                foreach (var candidate in FindAssembliesRecursive(
                    subDirectory,
                    fileName,
                    excludedTiaVersions))
                {
                    yield return candidate;
                }
            }
        }
    }
}
