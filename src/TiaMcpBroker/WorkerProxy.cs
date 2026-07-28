using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TiaMcpBroker
{
    internal static class WorkerProxy
    {
        public static async Task<int> RunAsync(
            string workerPath,
            IReadOnlyList<string> workerArguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = workerPath,
                Arguments = BuildArgumentString(workerArguments),
                WorkingDirectory = Path.GetDirectoryName(workerPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                try
                {
                    if (!process.Start())
                    {
                        throw new BrokerException(
                            BrokerExitCode.WorkerLaunchFailed,
                            $"Windows did not start the worker '{workerPath}'.");
                    }
                }
                catch (Exception exception) when (
                    exception is Win32Exception ||
                    exception is InvalidOperationException)
                {
                    throw new BrokerException(
                        BrokerExitCode.WorkerLaunchFailed,
                        $"The exact worker could not be launched: {workerPath}. {exception.Message}",
                        exception);
                }

                EventHandler processExitHandler = (sender, eventArgs) => TryKill(process);
                ConsoleCancelEventHandler cancelHandler = (sender, eventArgs) => TryKill(process);
                var processExitHandlerRegistered = false;
                var cancelHandlerRegistered = false;

                try
                {
                    // This is best-effort direct-child cleanup. A later Windows job-object
                    // implementation is needed to guarantee cleanup after abrupt termination.
                    AppDomain.CurrentDomain.ProcessExit += processExitHandler;
                    processExitHandlerRegistered = true;
                    Console.CancelKeyPress += cancelHandler;
                    cancelHandlerRegistered = true;

                    var inputTask = PumpInputAsync(
                        Console.OpenStandardInput(),
                        process.StandardInput.BaseStream);
                    ObserveFault(inputTask);

                    var outputTask = PumpOutputAsync(
                        process.StandardOutput.BaseStream,
                        Console.OpenStandardOutput());
                    var errorTask = PumpOutputAsync(
                        process.StandardError.BaseStream,
                        Console.OpenStandardError());

                    var exitTask = WaitForExitAsync(process);
                    return await MonitorWorkerAsync(
                        process,
                        exitTask,
                        outputTask,
                        errorTask).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    TryKill(process);
                    throw new BrokerException(
                        BrokerExitCode.ProxyFailed,
                        $"The standard-stream proxy failed for worker '{workerPath}'. {exception.Message}",
                        exception);
                }
                finally
                {
                    if (processExitHandlerRegistered)
                    {
                        AppDomain.CurrentDomain.ProcessExit -= processExitHandler;
                    }

                    if (cancelHandlerRegistered)
                    {
                        Console.CancelKeyPress -= cancelHandler;
                    }

                    TryKill(process);
                }
            }
        }

        private static async Task<int> MonitorWorkerAsync(
            Process process,
            Task<int> exitTask,
            Task outputTask,
            Task errorTask)
        {
            var pendingOutputTasks = new List<Task>
            {
                outputTask,
                errorTask
            };

            try
            {
                while (!exitTask.IsCompleted && pendingOutputTasks.Count > 0)
                {
                    var lifecycleTasks = new List<Task>(pendingOutputTasks.Count + 1)
                    {
                        exitTask
                    };
                    lifecycleTasks.AddRange(pendingOutputTasks);

                    var completedTask = await Task.WhenAny(lifecycleTasks).ConfigureAwait(false);
                    if (ReferenceEquals(completedTask, exitTask))
                    {
                        break;
                    }

                    pendingOutputTasks.Remove(completedTask);

                    // Await each pump as soon as it completes so a broken stdout or
                    // stderr destination cannot leave the worker blocked on a full pipe.
                    await completedTask.ConfigureAwait(false);
                }

                var exitCode = await exitTask.ConfigureAwait(false);
                await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
                return exitCode;
            }
            catch
            {
                TryKill(process);
                await ObserveLifecycleTasksAsync(
                    exitTask,
                    outputTask,
                    errorTask).ConfigureAwait(false);
                throw;
            }
        }

        private static async Task ObserveLifecycleTasksAsync(params Task[] tasks)
        {
            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch
            {
                foreach (var task in tasks)
                {
                    if (task.IsFaulted)
                    {
                        _ = task.Exception;
                    }
                }
            }
        }

        private static async Task PumpInputAsync(Stream source, Stream destination)
        {
            try
            {
                await source.CopyToAsync(destination).ConfigureAwait(false);
                await destination.FlushAsync().ConfigureAwait(false);
            }
            catch (IOException)
            {
                // The worker may close its input before the MCP client closes the broker's input.
            }
            catch (ObjectDisposedException)
            {
                // The worker exited while an input copy was pending.
            }
            finally
            {
                destination.Dispose();
            }
        }

        private static async Task PumpOutputAsync(Stream source, Stream destination)
        {
            await source.CopyToAsync(destination).ConfigureAwait(false);
            await destination.FlushAsync().ConfigureAwait(false);
        }

        private static Task<int> WaitForExitAsync(Process process)
        {
            var completion = new TaskCompletionSource<int>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            process.EnableRaisingEvents = true;
            process.Exited += (sender, eventArgs) =>
            {
                try
                {
                    completion.TrySetResult(process.ExitCode);
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            };

            if (process.HasExited)
            {
                completion.TrySetResult(process.ExitCode);
            }

            return completion.Task;
        }

        private static string BuildArgumentString(IEnumerable<string> arguments)
        {
            return string.Join(" ", arguments.Select(QuoteArgument));
        }

        private static string QuoteArgument(string argument)
        {
            if (argument.Length > 0 &&
                argument.All(character =>
                    !char.IsWhiteSpace(character) &&
                    character != '"'))
            {
                return argument;
            }

            var result = new StringBuilder();
            result.Append('"');
            var backslashCount = 0;

            foreach (var character in argument)
            {
                if (character == '\\')
                {
                    backslashCount++;
                    continue;
                }

                if (character == '"')
                {
                    result.Append('\\', (backslashCount * 2) + 1);
                    result.Append('"');
                    backslashCount = 0;
                    continue;
                }

                result.Append('\\', backslashCount);
                result.Append(character);
                backslashCount = 0;
            }

            result.Append('\\', backslashCount * 2);
            result.Append('"');
            return result.ToString();
        }

        private static void ObserveFault(Task task)
        {
            task.ContinueWith(
                completedTask =>
                {
                    _ = completedTask.Exception;
                },
                TaskContinuationOptions.ExecuteSynchronously |
                TaskContinuationOptions.OnlyOnFaulted);
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (Win32Exception)
            {
            }
        }
    }
}
