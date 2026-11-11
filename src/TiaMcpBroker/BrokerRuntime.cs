using System;

namespace TiaMcpBroker
{
    internal enum BrokerAccessProfile
    {
        Read,
        ReadWrite
    }

    internal enum BrokerExitCode
    {
        Success = 0,
        InvalidArguments = 2,
        InstallationResolutionFailed = 3,
        WorkerNotFound = 4,
        WorkerLaunchFailed = 5,
        ProxyFailed = 6
    }

    internal static class BuildIdentity
    {
#if TIA_MCP_BROKER_READ_WRITE
        public const BrokerAccessProfile AccessProfile = BrokerAccessProfile.ReadWrite;
#else
        public const BrokerAccessProfile AccessProfile = BrokerAccessProfile.Read;
#endif

        public static string AccessProfileArgument => AccessProfile.ToString();
    }

    internal sealed class BrokerException : Exception
    {
        public BrokerException(BrokerExitCode exitCode, string message)
            : base(message)
        {
            ExitCode = exitCode;
        }

        public BrokerException(BrokerExitCode exitCode, string message, Exception innerException)
            : base(message, innerException)
        {
            ExitCode = exitCode;
        }

        public BrokerExitCode ExitCode { get; }
    }
}
