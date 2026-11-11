using System;
using System.Collections.Generic;

namespace TiaMcpBroker
{
    internal enum TiaVersionRequest
    {
        Auto = 0,
        V17 = 17,
        V18 = 18,
        V19 = 19,
        V20 = 20,
        V21 = 21
    }

    internal sealed class BrokerOptions
    {
        private readonly List<string> workerArguments = new List<string>();

        public TiaVersionRequest TiaVersion { get; private set; } = TiaVersionRequest.Auto;
        public bool ShowHelp { get; private set; }
        public string? Error { get; private set; }
        public IReadOnlyList<string> WorkerArguments => workerArguments;

        public static BrokerOptions Parse(string[] args)
        {
            var options = new BrokerOptions();
            var versionWasSpecified = false;

            for (var index = 0; index < args.Length; index++)
            {
                var argument = args[index];
                switch (argument.ToLowerInvariant())
                {
                    case "-h":
                    case "-help":
                    case "--help":
                        options.ShowHelp = true;
                        break;

                    case "-tia-version":
                    case "--tia-version":
                        if (!TryReadValue(args, ref index, out var versionValue))
                        {
                            options.Error = $"{argument} requires Auto or V17 through V21.";
                            return options;
                        }

                        if (!TryParseVersion(versionValue, allowAuto: true, out var requestedVersion))
                        {
                            options.Error =
                                $"Invalid TIA Portal version '{versionValue}'. Use Auto or V17 through V21.";
                            return options;
                        }

                        if (!TrySetVersion(options, requestedVersion, ref versionWasSpecified))
                        {
                            return options;
                        }
                        break;

                    case "-tia-major-version":
                    case "--tia-major-version":
                        if (!TryReadValue(args, ref index, out var majorVersionValue))
                        {
                            options.Error = $"{argument} requires a number from 17 through 21.";
                            return options;
                        }

                        if (!TryParseVersion(majorVersionValue, allowAuto: false, out var numericVersion))
                        {
                            options.Error =
                                $"Invalid TIA Portal major version '{majorVersionValue}'. Use 17 through 21.";
                            return options;
                        }

                        if (!TrySetVersion(options, numericVersion, ref versionWasSpecified))
                        {
                            return options;
                        }
                        break;

                    case "-access-profile":
                    case "--access-profile":
                    case "-profile":
                    case "--profile":
                        if (!TryReadValue(args, ref index, out var accessProfileValue))
                        {
                            options.Error = $"{argument} requires Read or ReadWrite.";
                            return options;
                        }

                        if (!TryParseAccessProfile(accessProfileValue, out var requestedAccessProfile))
                        {
                            options.Error =
                                $"Invalid access profile '{accessProfileValue}'. Use Read or ReadWrite.";
                            return options;
                        }

                        if (requestedAccessProfile != BuildIdentity.AccessProfile)
                        {
                            options.Error =
                                $"This broker is fixed to {BuildIdentity.AccessProfileArgument}; " +
                                $"{requestedAccessProfile} requires the other broker build.";
                            return options;
                        }
                        break;

                    case "--":
                        for (index++; index < args.Length; index++)
                        {
                            if (IsReservedWorkerArgument(args[index]))
                            {
                                options.Error =
                                    $"Worker argument '{args[index]}' cannot override the broker's fixed " +
                                    "version or access profile.";
                                return options;
                            }

                            options.workerArguments.Add(args[index]);
                        }
                        return options;

                    default:
                        if (IsReservedWorkerArgument(argument))
                        {
                            options.Error =
                                $"Worker argument '{argument}' cannot override the broker's fixed " +
                                "version or access profile.";
                            return options;
                        }

                        options.workerArguments.Add(argument);
                        break;
                }
            }

            return options;
        }

        public static string GetHelpText()
        {
            return string.Join(
                Environment.NewLine,
                $"TIA Portal MCP Broker ({BuildIdentity.AccessProfileArgument} build)",
                string.Empty,
                "Usage:",
                "  TiaPortalMcp.exe [broker options] [worker options]",
                string.Empty,
                "Broker options:",
                "  --tia-version <Auto|V17|V18|V19>",
                "      Auto succeeds only when exactly one valid supported installation is present.",
                "      V20 is planned for a later alpha; V21 is unsupported in this release.",
                "  --tia-major-version <17|18|19>",
                "      Legacy numeric alias for an exact version.",
                "  --access-profile <Read|ReadWrite>",
                "      Accepted only when it matches this broker's fixed build profile.",
                "  --help",
                "      Show this help without starting a worker.",
                string.Empty,
                $"This executable is permanently bound to the {BuildIdentity.AccessProfileArgument} profile.",
                "Other arguments are forwarded to the worker. Version and profile overrides are rejected.",
                string.Empty,
                "Expected layout:",
                "  TiaPortalMcp.exe",
                "  workers\\v17\\TiaMcpServer.exe",
                "  workers\\v18\\TiaMcpServer.exe",
                "  workers\\v19\\TiaMcpServer.exe",
                string.Empty,
                "Broker exit codes:",
                "  0  Help displayed or worker succeeded",
                "  2  Invalid broker arguments",
                "  3  TIA Portal installation resolution failed",
                "  4  Exact worker executable was not found",
                "  5  Worker process could not be launched",
                "  6  Standard-stream proxy failed",
                "  Other values are propagated unchanged from the worker.");
        }

        private static bool TrySetVersion(
            BrokerOptions options,
            TiaVersionRequest requestedVersion,
            ref bool versionWasSpecified)
        {
            if (versionWasSpecified && options.TiaVersion != requestedVersion)
            {
                options.Error =
                    $"Conflicting TIA Portal versions were supplied: {options.TiaVersion} and {requestedVersion}.";
                return false;
            }

            options.TiaVersion = requestedVersion;
            versionWasSpecified = true;
            return true;
        }

        private static bool TryReadValue(string[] args, ref int index, out string value)
        {
            if (index + 1 >= args.Length)
            {
                value = string.Empty;
                return false;
            }

            index++;
            value = args[index];
            return true;
        }

        private static bool TryParseVersion(
            string value,
            bool allowAuto,
            out TiaVersionRequest version)
        {
            if (allowAuto && string.Equals(value, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                version = TiaVersionRequest.Auto;
                return true;
            }

            var numericValue = value;
            if (value.StartsWith("V", StringComparison.OrdinalIgnoreCase))
            {
                numericValue = value.Substring(1);
            }

            if (!int.TryParse(numericValue, out var majorVersion))
            {
                version = TiaVersionRequest.Auto;
                return false;
            }

            switch (majorVersion)
            {
                case 17:
                    version = TiaVersionRequest.V17;
                    return true;
                case 18:
                    version = TiaVersionRequest.V18;
                    return true;
                case 19:
                    version = TiaVersionRequest.V19;
                    return true;
                case 20:
                    version = TiaVersionRequest.V20;
                    return true;
                case 21:
                    version = TiaVersionRequest.V21;
                    return true;
                default:
                    version = TiaVersionRequest.Auto;
                    return false;
            }
        }

        private static bool IsReservedWorkerArgument(string argument)
        {
            switch (argument.ToLowerInvariant())
            {
                case "-tia-version":
                case "--tia-version":
                case "-tia-major-version":
                case "--tia-major-version":
                case "-access-profile":
                case "--access-profile":
                case "-profile":
                case "--profile":
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseAccessProfile(
            string value,
            out BrokerAccessProfile accessProfile)
        {
            switch (value.ToLowerInvariant())
            {
                case "read":
                case "readonly":
                case "read-only":
                    accessProfile = BrokerAccessProfile.Read;
                    return true;
                case "readwrite":
                case "read-write":
                case "rw":
                    accessProfile = BrokerAccessProfile.ReadWrite;
                    return true;
                default:
                    accessProfile = BrokerAccessProfile.Read;
                    return false;
            }
        }
    }
}
