using System;
using System.IO;

namespace TiaMcpServer.Security
{
    public static class OutputPathPolicy
    {
        private static readonly object SyncRoot = new object();
        private static string? outputRoot;

        public static string OutputRoot
        {
            get
            {
                lock (SyncRoot)
                {
                    return outputRoot
                        ?? throw new InvalidOperationException("The output path policy has not been configured.");
                }
            }
        }

        public static void Configure(string? configuredRoot)
        {
            var candidate = string.IsNullOrWhiteSpace(configuredRoot)
                ? GetDefaultOutputRoot()
                : configuredRoot;
            var resolvedRoot = Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(candidate));
            RejectExistingReparsePoints(resolvedRoot, nameof(configuredRoot));

            lock (SyncRoot)
            {
                if (outputRoot != null &&
                    !string.Equals(outputRoot, resolvedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"The output root is already locked to '{outputRoot}'.");
                }

                outputRoot = resolvedRoot;
            }
        }

        public static string ResolveDirectory(string requestedPath)
        {
            if (requestedPath == null)
            {
                throw new ArgumentNullException(nameof(requestedPath));
            }

            var root = OutputRoot;
            var expandedPath = Environment.ExpandEnvironmentVariables(requestedPath.Trim());
            var candidate = string.IsNullOrWhiteSpace(expandedPath)
                ? root
                : Path.IsPathRooted(expandedPath)
                    ? expandedPath
                    : Path.Combine(root, expandedPath);
            var resolvedPath = Path.GetFullPath(candidate);

            if (!IsWithinRoot(root, resolvedPath))
            {
                throw new ArgumentException(
                    $"Output path must remain within the configured output root '{root}'.",
                    nameof(requestedPath));
            }

            RejectReparsePoints(root, resolvedPath, nameof(requestedPath));
            return resolvedPath;
        }

        private static void RejectReparsePoints(
            string root,
            string candidate,
            string parameterValue)
        {
            var relativePath = candidate.Substring(root.Length).TrimStart(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var currentPath = root;

            foreach (var segment in relativePath.Split(
                new[]
                {
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                },
                StringSplitOptions.RemoveEmptyEntries))
            {
                currentPath = Path.Combine(currentPath, segment);

                if (!Directory.Exists(currentPath) && !File.Exists(currentPath))
                {
                    continue;
                }

                if ((File.GetAttributes(currentPath) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new ArgumentException(
                        $"Output path cannot pass through the reparse point '{currentPath}'.",
                        parameterValue);
                }
            }
        }

        private static void RejectExistingReparsePoints(
            string path,
            string parameterName)
        {
            var pathRoot = Path.GetPathRoot(path);
            if (string.IsNullOrWhiteSpace(pathRoot))
            {
                throw new ArgumentException(
                    $"Output path has no Windows path root: '{path}'.",
                    parameterName);
            }

            var relativePath = path.Substring(pathRoot.Length);
            var currentPath = pathRoot;
            foreach (var segment in relativePath.Split(
                new[]
                {
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                },
                StringSplitOptions.RemoveEmptyEntries))
            {
                currentPath = Path.Combine(currentPath, segment);
                if (!Directory.Exists(currentPath) && !File.Exists(currentPath))
                {
                    continue;
                }

                if ((File.GetAttributes(currentPath) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new ArgumentException(
                        $"Output root cannot pass through the reparse point '{currentPath}'.",
                        parameterName);
                }
            }
        }

        private static bool IsWithinRoot(string root, string candidate)
        {
            if (string.Equals(root, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var rootWithSeparator = root.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetDefaultOutputRoot()
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(documents))
            {
                documents = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }

            if (string.IsNullOrWhiteSpace(documents))
            {
                throw new InvalidOperationException(
                    "No default output directory is available. Set --output-root explicitly.");
            }

            return Path.Combine(documents, "TIA Portal MCP", "Exports");
        }
    }
}
