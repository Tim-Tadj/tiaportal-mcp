using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Runtime;
using TiaMcpServer.Security;
using TiaMcpServer.Siemens;

namespace TiaMcpServer
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var options = CliOptions.ParseArgs(args);

            if (options.ShowHelp)
            {
                Console.Out.WriteLine(CliOptions.Usage);
                Console.Out.WriteLine();
                Console.Out.WriteLine($"Current worker: {WorkerBuild.Current}");
                return 0;
            }

            if (options.Error != null)
            {
                WriteStartupError(options.Error);
                return 2;
            }

            try
            {
                OutputPathPolicy.Configure(options.OutputRoot);
            }
            catch (Exception exception)
            {
                WriteStartupError($"Invalid output root: {exception.Message}");
                return 3;
            }

            RuntimeSelection runtimeSelection;
            try
            {
                var broker = new RuntimeBroker(new TiaInstallationDetector());
                var resolution = broker.Resolve(options.TiaVersion, options.AccessProfile);

                // The resolver is deliberately separate from the current worker binding.
                // A future dependency-free supervisor can launch an isolated exact-version
                // worker here instead of binding the request to its own process.
                runtimeSelection = WorkerBuild.Current.Bind(resolution);
                RuntimeSelectionLock.Lock(runtimeSelection);
            }
            catch (RuntimeSelectionException exception)
            {
                WriteStartupError(exception.Message);
                return 4;
            }

            Engineering.TiaMajorVersion = runtimeSelection.TiaMajorVersion;

            try
            {
                if (runtimeSelection.TiaMajorVersion < 20)
                {
                    AppDomain.CurrentDomain.AssemblyResolve += Engineering.Resolver;
                }
                else
                {
                    Openness.Initialize(runtimeSelection.TiaMajorVersion);
                }
            }
            catch (Exception exception)
            {
                WriteStartupError(
                    $"Failed to initialise the exact TIA Portal V{runtimeSelection.TiaMajorVersion} " +
                    $"{runtimeSelection.AccessProfile} worker: {exception.Message}");
                return 5;
            }

            // Ensure user is in user group 'Siemens TIA Openness'
            if (await Openness.IsUserInGroup())
            {
                await RunStdioHost(options, runtimeSelection);
                return 0;
            }

            WriteStartupError("User is not in the required 'Siemens TIA Openness' group.");
            return 6;
        }

        public static async Task RunStdioHost(
            CliOptions? options,
            RuntimeSelection? runtimeSelection = null)
        {
            var builder = Host.CreateEmptyApplicationBuilder(settings: null);
            if (builder != null)
            {
                if (options != null && options.Logging != null)
                {
                    switch (options.Logging)
                    {
                        case 1:
                            // ATTENTION: For STDIO, logs must go to stderr!
                            builder.Logging.AddConsole(options =>
                            {
                                options.LogToStandardErrorThreshold = LogLevel.Trace;
                            });
                            break;

                        case 2:
                            // Visual Studio Debug Output / Sysinternals.DebugView
                            builder.Logging.AddDebug();
                            builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
                            builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Information);
                            builder.Logging.AddFilter("TiaMcpServer", LogLevel.Debug);

                            // Log Level for Debug Output
                            builder.Logging.SetMinimumLevel(LogLevel.Debug);
                            break;

                        case 3:
                            // Windows Event Log
                            builder.Logging.AddEventLog();
                            break;

                        default:
                            // no logging
                            break;
                    }
                }

                builder.Services
                    .AddMcpServer()
                    .WithStdioServerTransport()
                    .WithTools<McpServer>()
                    .WithTools<McpListTools>()
                    .WithPrompts<McpPrompts>()
                    .WithRequestFilters(filters =>
                    {
                        filters.AddCallToolFilter(next => async (context, cancellationToken) =>
                        {
                            var operationGate =
                                context.Services?.GetRequiredService<TiaOperationGate>() ??
                                throw new InvalidOperationException(
                                    "The TIA Portal operation gate is unavailable.");

                            return await operationGate.RunAsync(
                                async () => await next(context, cancellationToken)
                                    .ConfigureAwait(false),
                                cancellationToken).ConfigureAwait(false);
                        });
                    });

                // Register the Portal service for dependency injection
                builder.Services.AddSingleton<Portal>();
                builder.Services.AddSingleton<TiaOperationGate>();

                var host = builder.Build();

                // Set the service provider for the MCP server, to retrieve Portal with injected logger
                McpServer.SetServiceProvider(host.Services);

                // Set the logger for the MCP server
                McpServer.Logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("McpServer");

                // log a bit of information about the server start
                if (options != null && options.Logging != null && options.Logging > 0)
                {
                    var logger = host.Services.GetRequiredService<ILogger<Program>>();

                    logger.LogInformation($"=== TIA Portal MCP Server '{DateTime.Now.ToShortTimeString()}' ===");

                    if (runtimeSelection != null)
                    {
                        logger.LogInformation(
                            "Runtime locked to TIA Portal V{TiaMajorVersion} with {AccessProfile} access",
                            runtimeSelection.TiaMajorVersion,
                            runtimeSelection.AccessProfile);
                    }

                    switch (options.Logging)
                    {
                        case 1:
                            logger.LogInformation("Logging to stderr");
                            break;
                        case 2:
                            logger.LogInformation("Logging to debug output");
                            break;
                        case 3:
                            logger.LogInformation("Logging to Windows event log");
                            break;
                    }
                }

                await host.RunAsync();
            }

        }

        private static void WriteStartupError(string message)
        {
            Console.Error.WriteLine($"TIA Portal MCP Server: {message}");
        }
    }
}
