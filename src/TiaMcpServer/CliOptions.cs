using System;
using TiaMcpServer.Runtime;

namespace TiaMcpServer
{
    public class CliOptions
    {
        public TiaVersionSelection TiaVersion { get; set; } = TiaVersionSelection.Auto;
        public int? TiaMajorVersion
        {
            get
            {
                return TiaVersion == TiaVersionSelection.Auto
                    ? (int?)null
                    : (int)TiaVersion;
            }
            set
            {
                if (!value.HasValue)
                {
                    TiaVersion = TiaVersionSelection.Auto;
                    return;
                }

                if (!TryGetVersion(value.Value, out var version))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "TIA Portal major version must be between 17 and 21.");
                }

                TiaVersion = version;
            }
        }

        public AccessProfile? AccessProfile { get; set; }
        public string? OutputRoot { get; set; }
        public int? Logging { get; set; } // "stdio" or "http"
        public bool ShowHelp { get; private set; }
        public string? Error { get; private set; }

        public static string Usage => string.Join(
            Environment.NewLine,
            "TIA Portal MCP Server",
            string.Empty,
            "Options:",
            "  --tia-version <Auto|V17|V18|V19|V20|V21>",
            "      Select an installed TIA Portal version. Auto requires exactly one valid installation.",
            "  --tia-major-version <17|18|19|20|21>",
            "      Legacy numeric alias for --tia-version.",
            "  --access-profile <Read|ReadWrite>",
            "      Select the exact access-profile worker. If omitted, the worker's built profile is used.",
            "  --output-root <path>",
            "      Contain all exported files beneath this directory.",
            "  --logging <number>",
            "      Preserve the existing logging selector.",
            "  --help",
            "      Show this help.");

        public static CliOptions ParseArgs(string[] args)
        {
            var options = new CliOptions();
            var versionWasSpecified = false;
            var accessProfileWasSpecified = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "-h":
                    case "--help":
                    case "-help":
                        options.ShowHelp = true;
                        break;

                    case "-tia-version":
                    case "--tia-version":
                        if (!TryReadValue(args, ref i, out var tiaVersionValue))
                        {
                            options.Error = $"{args[i]} requires Auto or V17 through V21.";
                            return options;
                        }

                        if (!TryParseVersion(tiaVersionValue, out var tiaVersion))
                        {
                            options.Error =
                                $"Invalid TIA Portal version '{tiaVersionValue}'. Use Auto or V17 through V21.";
                            return options;
                        }

                        if (versionWasSpecified && options.TiaVersion != tiaVersion)
                        {
                            options.Error =
                                $"Conflicting TIA Portal versions were supplied: {options.TiaVersion} and {tiaVersion}.";
                            return options;
                        }

                        options.TiaVersion = tiaVersion;
                        versionWasSpecified = true;
                        break;

                    case "-tia-major-version":
                    case "--tia-major-version":
                        if (!TryReadValue(args, ref i, out var majorVersionValue))
                        {
                            options.Error = $"{args[i]} requires a number from 17 through 21.";
                            return options;
                        }

                        if (!int.TryParse(majorVersionValue, out var majorVersion) ||
                            !TryGetVersion(majorVersion, out var numericVersion))
                        {
                            options.Error =
                                $"Invalid TIA Portal major version '{majorVersionValue}'. Use 17 through 21.";
                            return options;
                        }

                        if (versionWasSpecified && options.TiaVersion != numericVersion)
                        {
                            options.Error =
                                $"Conflicting TIA Portal versions were supplied: {options.TiaVersion} and {numericVersion}.";
                            return options;
                        }

                        options.TiaVersion = numericVersion;
                        versionWasSpecified = true;
                        break;

                    case "-access-profile":
                    case "--access-profile":
                    case "-profile":
                    case "--profile":
                        if (!TryReadValue(args, ref i, out var accessProfileValue))
                        {
                            options.Error = $"{args[i]} requires Read or ReadWrite.";
                            return options;
                        }

                        if (!TryParseAccessProfile(accessProfileValue, out var accessProfile))
                        {
                            options.Error =
                                $"Invalid access profile '{accessProfileValue}'. Use Read or ReadWrite.";
                            return options;
                        }

                        if (accessProfileWasSpecified && options.AccessProfile != accessProfile)
                        {
                            options.Error =
                                $"Conflicting access profiles were supplied: {options.AccessProfile} and {accessProfile}.";
                            return options;
                        }

                        options.AccessProfile = accessProfile;
                        accessProfileWasSpecified = true;
                        break;

                    case "-logging":
                    case "--logging":
                        if (!TryReadValue(args, ref i, out var loggingValue))
                        {
                            options.Error = $"{args[i]} requires a numeric value.";
                            return options;
                        }

                        if (!int.TryParse(loggingValue, out var logging))
                        {
                            options.Error = $"Invalid logging value '{loggingValue}'. Use a number.";
                            return options;
                        }

                        options.Logging = logging;
                        break;

                    case "-output-root":
                    case "--output-root":
                        if (!TryReadValue(args, ref i, out var outputRoot))
                        {
                            options.Error = $"{args[i]} requires a directory path.";
                            return options;
                        }

                        options.OutputRoot = outputRoot;
                        break;

                    default:
                        options.Error = $"Unknown option '{args[i]}'. Use --help to list supported options.";
                        return options;
                }
            }

            return options;
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

        private static bool TryParseVersion(string value, out TiaVersionSelection version)
        {
            if (string.Equals(value, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                version = TiaVersionSelection.Auto;
                return true;
            }

            var numericValue = value;
            if (value.StartsWith("V", StringComparison.OrdinalIgnoreCase))
            {
                numericValue = value.Substring(1);
            }

            if (!int.TryParse(numericValue, out var majorVersion))
            {
                version = TiaVersionSelection.Auto;
                return false;
            }

            return TryGetVersion(majorVersion, out version);
        }

        private static bool TryGetVersion(int majorVersion, out TiaVersionSelection version)
        {
            switch (majorVersion)
            {
                case 17:
                    version = TiaVersionSelection.V17;
                    return true;
                case 18:
                    version = TiaVersionSelection.V18;
                    return true;
                case 19:
                    version = TiaVersionSelection.V19;
                    return true;
                case 20:
                    version = TiaVersionSelection.V20;
                    return true;
                case 21:
                    version = TiaVersionSelection.V21;
                    return true;
                default:
                    version = TiaVersionSelection.Auto;
                    return false;
            }
        }

        private static bool TryParseAccessProfile(string value, out AccessProfile accessProfile)
        {
            switch (value.ToLowerInvariant())
            {
                case "read":
                case "readonly":
                case "read-only":
                    accessProfile = Runtime.AccessProfile.Read;
                    return true;
                case "readwrite":
                case "read-write":
                case "rw":
                    accessProfile = Runtime.AccessProfile.ReadWrite;
                    return true;
                default:
                    accessProfile = Runtime.AccessProfile.Read;
                    return false;
            }
        }
    }
}
