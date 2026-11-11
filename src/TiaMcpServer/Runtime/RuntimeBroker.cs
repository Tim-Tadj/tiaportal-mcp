using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Runtime
{
    public sealed class RuntimeBroker
    {
        private readonly ITiaInstallationDetector installationDetector;

        public RuntimeBroker(ITiaInstallationDetector installationDetector)
        {
            this.installationDetector = installationDetector
                ?? throw new ArgumentNullException(nameof(installationDetector));
        }

        /// <summary>
        /// Resolves an installed exact TIA Portal version without loading Siemens assemblies.
        /// A future supervisor can use this resolution to launch the matching isolated worker.
        /// </summary>
        public RuntimeResolution Resolve(
            TiaVersionSelection requestedVersion,
            AccessProfile? requestedAccessProfile)
        {
            var scan = installationDetector.Scan();
            TiaInstallation installation;

            if (requestedVersion == TiaVersionSelection.Auto)
            {
                installation = ResolveAutomaticVersion(scan);
            }
            else
            {
                installation = ResolveExactVersion(scan, (int)requestedVersion);
            }

            return new RuntimeResolution(installation, requestedAccessProfile);
        }

        private static TiaInstallation ResolveAutomaticVersion(TiaInstallationScan scan)
        {
            if (scan.Installations.Count == 0)
            {
                throw new RuntimeSelectionException(
                    "No valid TIA Portal Openness installation was found for V17 to V21." +
                    FormatInvalidInstallations(scan.InvalidInstallations));
            }

            if (scan.Installations.Count > 1)
            {
                var detectedVersions = string.Join(
                    ", ",
                    scan.Installations.Select(FormatInstallation));

                throw new RuntimeSelectionException(
                    $"TIA Portal version selection is ambiguous. Detected: {detectedVersions}. " +
                    "Choose one explicitly with --tia-version V<major>, for example --tia-version V20, " +
                    "or use the legacy " +
                    "--tia-major-version <17-21> option. No Siemens API has been initialised.");
            }

            return scan.Installations[0];
        }

        private static TiaInstallation ResolveExactVersion(
            TiaInstallationScan scan,
            int requestedMajorVersion)
        {
            var installation = scan.Installations.SingleOrDefault(
                candidate => candidate.MajorVersion == requestedMajorVersion);

            if (installation != null)
            {
                return installation;
            }

            var detectedVersions = scan.Installations.Count == 0
                ? "none"
                : string.Join(", ", scan.Installations.Select(FormatInstallation));

            throw new RuntimeSelectionException(
                $"TIA Portal V{requestedMajorVersion} was requested, but no valid V{requestedMajorVersion} " +
                $"Openness installation was found. Detected: {detectedVersions}." +
                FormatInvalidInstallations(scan.InvalidInstallations));
        }

        private static string FormatInstallation(TiaInstallation installation)
        {
            return $"V{installation.MajorVersion} ({installation.InstallPath})";
        }

        private static string FormatInvalidInstallations(IReadOnlyList<string> invalidInstallations)
        {
            if (invalidInstallations.Count == 0)
            {
                return string.Empty;
            }

            return " Invalid registry entries: " + string.Join("; ", invalidInstallations);
        }
    }
}
