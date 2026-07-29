using System.Collections.Generic;
using System.Linq;

namespace TiaMcpBroker
{
    internal sealed class TiaVersionResolver
    {
        private readonly TiaInstallationDetector installationDetector;

        public TiaVersionResolver(TiaInstallationDetector installationDetector)
        {
            this.installationDetector = installationDetector;
        }

        public int Resolve(TiaVersionRequest request)
        {
            if (request == TiaVersionRequest.V21)
            {
                throw new BrokerException(
                    BrokerExitCode.InstallationResolutionFailed,
                    "TIA Portal V21 is recognised but is not included in this bundle. " +
                    "Install or select TIA Portal V17, V18, V19 or V20.");
            }

            var scan = installationDetector.Scan();

            if (request == TiaVersionRequest.Auto)
            {
                return ResolveAutomatic(scan);
            }

            return ResolveExact(scan, (int)request);
        }

        private static int ResolveAutomatic(TiaInstallationScan scan)
        {
            if (scan.Installations.Count == 0)
            {
                throw new BrokerException(
                    BrokerExitCode.InstallationResolutionFailed,
                    "No valid TIA Portal Openness installation was found for V17 to V20." +
                    FormatInvalidEntries(scan.InvalidEntries));
            }

            if (scan.Installations.Count > 1)
            {
                var detected = string.Join(", ", scan.Installations.Select(FormatInstallation));
                throw new BrokerException(
                    BrokerExitCode.InstallationResolutionFailed,
                    $"Auto version selection is ambiguous. Detected: {detected}. Choose one with " +
                    "--tia-version V<major>, for example --tia-version V20.");
            }

            return scan.Installations[0].MajorVersion;
        }

        private static int ResolveExact(TiaInstallationScan scan, int requestedVersion)
        {
            var installation = scan.Installations.SingleOrDefault(
                candidate => candidate.MajorVersion == requestedVersion);

            if (installation != null)
            {
                return installation.MajorVersion;
            }

            var detected = scan.Installations.Count == 0
                ? "none"
                : string.Join(", ", scan.Installations.Select(FormatInstallation));

            throw new BrokerException(
                BrokerExitCode.InstallationResolutionFailed,
                $"TIA Portal V{requestedVersion} was requested, but no valid V{requestedVersion} " +
                $"Openness installation was found. Detected: {detected}." +
                FormatInvalidEntries(scan.InvalidEntries));
        }

        private static string FormatInstallation(TiaInstallation installation)
        {
            return $"V{installation.MajorVersion} ({installation.InstallPath})";
        }

        private static string FormatInvalidEntries(IReadOnlyList<string> invalidEntries)
        {
            if (invalidEntries.Count == 0)
            {
                return string.Empty;
            }

            return " Invalid registry entries: " + string.Join("; ", invalidEntries);
        }
    }
}
