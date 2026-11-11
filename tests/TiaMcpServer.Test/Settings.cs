using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace TiaMcpServer.Test
{
    internal static class Settings
    {
        private const string ProjectPathToken = "$(TIA_MCP_TEST_PROJECT_PATH)";
        private const string SessionPathToken = "$(TIA_MCP_TEST_SESSION_PATH)";
        private const string OutputRootToken = "$(TIA_MCP_TEST_OUTPUT_ROOT)";

        private static readonly Lazy<string> RepositoryRootValue =
            new Lazy<string>(FindRepositoryRoot);
        private static readonly Lazy<string> OutputRootValue =
            new Lazy<string>(ResolveOutputRoot);
        private static readonly Lazy<string> ProjectPathValue =
            new Lazy<string>(ResolveProjectPath);
        private static readonly Lazy<string> SessionPathValue =
            new Lazy<string>(ResolveSessionPath);

        // MultiUser Project 'TestSession1'
        public const string Session1ProjectPath = SessionPathToken;
        public const string Session1PlcSoftwarePath = "PC-System_1/Software PLC_1";
        public const string Session1ExportPath = OutputRootToken + "\\TestSession1";

        // Local Project 'TestProject1'
        public const string Project1ProjectPath = ProjectPathToken;
        public const string Project1PathNew =
            OutputRootToken + "\\SaveAs\\TestProject1\\TestProject1";
        public const string Project1PlcSoftwarePath0 = "PLC_0";
        public const string Project1PlcSoftwarePath1 = "PC-System_0/Software PLC_0";
        public const string Project1PlcSoftwarePath2 = "Group1/PLC_1";
        public const string Project1PlcSoftwarePath3 = "Group1/PC-System_1/Software PLC_1";
        public const string Project1PlcSoftwarePath4 = "Group1/Group1.1/PLC_1.1";
        public const string Project1PlcSoftwarePath5 = "Group1/Group1.1/PC-System_1.1/Software PLC_1.1";
        public const string Project1PlcSoftwarePath6 = "Group1/Group1.1/Group1.1.1/PLC_1.1.1";
        public const string Project1PlcSoftwarePath7 = "Group1/Group1.1/Group1.1.1/PC-System_1.1.1/Software PLC_1.1.1";
        public const string Project1ExportPath0 = OutputRootToken + "\\Project1\\PLC_0";
        public const string Project1ExportPath1 = OutputRootToken + "\\Project1\\PC-System_0\\Software PLC_0";
        public const string Project1ExportPath2 = OutputRootToken + "\\Project1\\Group1\\PLC_1";
        public const string Project1ExportPath3 = OutputRootToken + "\\Project1\\Group1\\PC-System_1\\Software PLC_1";
        public const string Project1ExportPath4 = OutputRootToken + "\\Project1\\Group1\\Group1.1\\PLC_1.1";
        public const string Project1ExportPath5 = OutputRootToken + "\\Project1\\Group1\\Group1.1\\PC-System_1.1\\Software PLC_1.1";
        public const string Project1ExportPath6 = OutputRootToken + "\\Project1\\Group1\\Group1.1\\Group1.1.1\\PLC_1.1.1";
        public const string Project1ExportPath7 = OutputRootToken + "\\Project1\\Group1\\Group1.1\\Group1.1.1\\PC-System_1.1.1\\Software PLC_1.1.1";

        public static string OutputRoot => OutputRootValue.Value;

        public static void PrepareTestAssets()
        {
            Directory.CreateDirectory(OutputRoot);

            if (!string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable("TIA_MCP_TEST_PROJECT_PATH")) ||
                File.Exists(ProjectPathValue.Value))
            {
                return;
            }

            var archivePath = Directory
                .EnumerateFiles(AssetsDirectory, "TestProject1.zap*", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (archivePath == null)
            {
                return;
            }

            var destinationDirectory = Path.GetDirectoryName(ProjectPathValue.Value);
            if (string.IsNullOrWhiteSpace(destinationDirectory))
            {
                throw new InvalidOperationException(
                    "Could not determine the temporary test-project directory.");
            }

            Directory.CreateDirectory(destinationDirectory);
            ZipFile.ExtractToDirectory(archivePath, destinationDirectory);
        }

        public static object ResolveDataValue(object value)
        {
            var text = value as string;
            if (text == null)
            {
                return value;
            }

            var containsPathToken =
                text.Contains(ProjectPathToken) ||
                text.Contains(SessionPathToken) ||
                text.Contains(OutputRootToken);
            if (!containsPathToken)
            {
                return value;
            }

            return Path.GetFullPath(
                text
                    .Replace(ProjectPathToken, ProjectPathValue.Value)
                    .Replace(SessionPathToken, SessionPathValue.Value)
                    .Replace(OutputRootToken, OutputRoot));
        }

        private static string AssetsDirectory =>
            Path.Combine(
                RepositoryRootValue.Value,
                "tests",
                "TiaMcpServer.Test",
                "assets");

        private static string ResolveOutputRoot()
        {
            var configuredPath =
                Environment.GetEnvironmentVariable("TIA_MCP_TEST_OUTPUT_ROOT");
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return ResolvePath(configuredPath);
            }

            return Path.Combine(
                Path.GetTempPath(),
                "TIA Portal MCP",
                "Tests",
                GetRepositoryIdentity());
        }

        private static string ResolveProjectPath()
        {
            var configuredPath =
                Environment.GetEnvironmentVariable("TIA_MCP_TEST_PROJECT_PATH");
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return ResolvePath(configuredPath);
            }

            return Path.Combine(
                OutputRoot,
                "TestAssets",
                "TestProject1",
                "TestProject1.ap20");
        }

        private static string ResolveSessionPath()
        {
            var configuredPath =
                Environment.GetEnvironmentVariable("TIA_MCP_TEST_SESSION_PATH");
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return ResolvePath(configuredPath);
            }

            var discoveredPath = Directory
                .EnumerateFiles(AssetsDirectory, "TestSession1*.als*", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            return discoveredPath ??
                Path.Combine(
                    AssetsDirectory,
                    "TestSession1",
                    "TestSession1_LS_1.als20");
        }

        private static string ResolvePath(string path)
        {
            var expandedPath =
                Environment.ExpandEnvironmentVariables(path.Trim());
            var combinedPath = Path.IsPathRooted(expandedPath)
                ? expandedPath
                : Path.Combine(RepositoryRootValue.Value, expandedPath);
            return Path.GetFullPath(combinedPath);
        }

        private static string FindRepositoryRoot()
        {
            var startDirectories = new[]
            {
                AppContext.BaseDirectory,
                Environment.CurrentDirectory
            };

            foreach (var startDirectory in startDirectories)
            {
                var currentDirectory = new DirectoryInfo(startDirectory);
                while (currentDirectory != null)
                {
                    if (File.Exists(
                        Path.Combine(currentDirectory.FullName, "TiaMcpServer.sln")))
                    {
                        return currentDirectory.FullName;
                    }

                    currentDirectory = currentDirectory.Parent;
                }
            }

            throw new InvalidOperationException(
                "Could not locate the repository root from the test process. " +
                "Run the tests from a working tree containing TiaMcpServer.sln.");
        }

        private static string GetRepositoryIdentity()
        {
            var canonicalRoot = RepositoryRootValue.Value
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToUpperInvariant();
            var pathBytes = Encoding.UTF8.GetBytes(canonicalRoot);

            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(pathBytes);
                return BitConverter
                    .ToString(hash, 0, 16)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }
    }
}
