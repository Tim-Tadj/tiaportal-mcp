using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace TiaMcpBroker
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            var options = BrokerOptions.Parse(args);

            if (options.ShowHelp)
            {
                Console.Out.WriteLine(BrokerOptions.GetHelpText());
                return (int)BrokerExitCode.Success;
            }

            if (options.Error != null)
            {
                WriteError(BrokerExitCode.InvalidArguments, options.Error);
                return (int)BrokerExitCode.InvalidArguments;
            }

            try
            {
                var resolver = new TiaVersionResolver(new TiaInstallationDetector());
                var tiaMajorVersion = resolver.Resolve(options.TiaVersion);
                var workerPath = GetWorkerPath(tiaMajorVersion);

                if (!File.Exists(workerPath))
                {
                    throw new BrokerException(
                        BrokerExitCode.WorkerNotFound,
                        $"The exact TIA Portal V{tiaMajorVersion} {BuildIdentity.AccessProfileArgument} " +
                        $"worker was not found: {workerPath}");
                }

                var workerArguments = new List<string>
                {
                    "--tia-version",
                    $"V{tiaMajorVersion}",
                    "--access-profile",
                    BuildIdentity.AccessProfileArgument
                };
                workerArguments.AddRange(options.WorkerArguments);

                return await WorkerProxy.RunAsync(workerPath, workerArguments).ConfigureAwait(false);
            }
            catch (BrokerException exception)
            {
                WriteError(exception.ExitCode, exception.Message);
                return (int)exception.ExitCode;
            }
            catch (Exception exception)
            {
                WriteError(
                    BrokerExitCode.ProxyFailed,
                    $"Unexpected broker failure: {exception.Message}");
                return (int)BrokerExitCode.ProxyFailed;
            }
        }

        private static string GetWorkerPath(int tiaMajorVersion)
        {
            return Path.GetFullPath(
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "workers",
                    $"v{tiaMajorVersion}",
                    "TiaMcpServer.exe"));
        }

        private static void WriteError(BrokerExitCode exitCode, string message)
        {
            Console.Error.WriteLine(
                $"TIA Portal MCP Broker [exit {(int)exitCode}]: {message}");
        }
    }
}
