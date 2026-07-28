using System;
using System.Collections.Generic;

namespace TiaMcpServer.Runtime
{
    public enum AccessProfile
    {
        Read,
        ReadWrite
    }

    public enum TiaVersionSelection
    {
        Auto = 0,
        V17 = 17,
        V18 = 18,
        V19 = 19,
        V20 = 20,
        V21 = 21
    }

    public sealed class TiaInstallation
    {
        public TiaInstallation(int majorVersion, string installPath)
        {
            MajorVersion = majorVersion;
            InstallPath = installPath ?? throw new ArgumentNullException(nameof(installPath));
        }

        public int MajorVersion { get; }
        public string InstallPath { get; }
    }

    public sealed class TiaInstallationScan
    {
        public TiaInstallationScan(
            IReadOnlyList<TiaInstallation> installations,
            IReadOnlyList<string> invalidInstallations)
        {
            Installations = installations ?? throw new ArgumentNullException(nameof(installations));
            InvalidInstallations = invalidInstallations ?? throw new ArgumentNullException(nameof(invalidInstallations));
        }

        public IReadOnlyList<TiaInstallation> Installations { get; }
        public IReadOnlyList<string> InvalidInstallations { get; }
    }

    public sealed class RuntimeResolution
    {
        public RuntimeResolution(TiaInstallation installation, AccessProfile? requestedAccessProfile)
        {
            Installation = installation ?? throw new ArgumentNullException(nameof(installation));
            RequestedAccessProfile = requestedAccessProfile;
        }

        public TiaInstallation Installation { get; }
        public AccessProfile? RequestedAccessProfile { get; }
    }

    public sealed class RuntimeSelection
    {
        public RuntimeSelection(int tiaMajorVersion, AccessProfile accessProfile, string installPath)
        {
            TiaMajorVersion = tiaMajorVersion;
            AccessProfile = accessProfile;
            InstallPath = installPath ?? throw new ArgumentNullException(nameof(installPath));
        }

        public int TiaMajorVersion { get; }
        public AccessProfile AccessProfile { get; }
        public string InstallPath { get; }
    }

    public sealed class WorkerBuild
    {
        private static readonly WorkerBuild current = new WorkerBuild(
            GetCompiledTiaMajorVersion(),
            GetCompiledAccessProfile());

        public WorkerBuild(int tiaMajorVersion, AccessProfile accessProfile)
        {
            TiaMajorVersion = tiaMajorVersion;
            AccessProfile = accessProfile;
        }

        /// <summary>
        /// Describes this executable, not a compatibility range.
        /// MSBuild supplies the exact TIA version and access-profile constants
        /// for each internal worker build.
        /// </summary>
        public static WorkerBuild Current => current;

        public int TiaMajorVersion { get; }
        public AccessProfile AccessProfile { get; }

        public RuntimeSelection Bind(RuntimeResolution resolution)
        {
            if (resolution == null)
            {
                throw new ArgumentNullException(nameof(resolution));
            }

            var requestedProfile = resolution.RequestedAccessProfile ?? AccessProfile;
            var requestedVersion = resolution.Installation.MajorVersion;

            if (requestedVersion != TiaMajorVersion)
            {
                throw new RuntimeSelectionException(
                    $"This executable is an exact TIA Portal V{TiaMajorVersion} {AccessProfile} worker and " +
                    $"cannot run the resolved V{requestedVersion} {requestedProfile} request. Install or launch " +
                    $"the V{requestedVersion} {requestedProfile} worker. This build is not used as " +
                    "a compatibility worker for other TIA Portal versions.");
            }

            if (requestedProfile != AccessProfile)
            {
                throw new RuntimeSelectionException(
                    $"This executable is an exact TIA Portal V{TiaMajorVersion} {AccessProfile} worker and " +
                    $"cannot provide the {requestedProfile} profile. Install or launch the V{TiaMajorVersion} " +
                    $"{requestedProfile} worker. Access profiles are worker identities, not runtime filtering.");
            }

            return new RuntimeSelection(
                resolution.Installation.MajorVersion,
                requestedProfile,
                resolution.Installation.InstallPath);
        }

        public override string ToString()
        {
            return $"TIA Portal V{TiaMajorVersion} {AccessProfile}";
        }

        private static int GetCompiledTiaMajorVersion()
        {
#if TIA_MCP_V17
            return 17;
#elif TIA_MCP_V18
            return 18;
#elif TIA_MCP_V19
            return 19;
#elif TIA_MCP_V20
            return 20;
#elif TIA_MCP_V21
            return 21;
#else
#error A TIA_MCP_V17 through TIA_MCP_V21 compilation constant is required.
#endif
        }

        private static AccessProfile GetCompiledAccessProfile()
        {
#if TIA_MCP_READ_WRITE
            return AccessProfile.ReadWrite;
#else
            return AccessProfile.Read;
#endif
        }
    }

    public static class RuntimeSelectionLock
    {
        private static readonly object syncRoot = new object();
        private static RuntimeSelection? current;

        public static RuntimeSelection Current
        {
            get
            {
                lock (syncRoot)
                {
                    return current
                        ?? throw new InvalidOperationException("The runtime selection has not been locked.");
                }
            }
        }

        public static void Lock(RuntimeSelection selection)
        {
            if (selection == null)
            {
                throw new ArgumentNullException(nameof(selection));
            }

            lock (syncRoot)
            {
                if (current != null)
                {
                    throw new InvalidOperationException(
                        $"The runtime is already locked to TIA Portal V{current.TiaMajorVersion} " +
                        $"{current.AccessProfile}.");
                }

                current = selection;
            }
        }
    }

    public sealed class RuntimeSelectionException : Exception
    {
        public RuntimeSelectionException(string message)
            : base(message)
        {
        }

        public RuntimeSelectionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
