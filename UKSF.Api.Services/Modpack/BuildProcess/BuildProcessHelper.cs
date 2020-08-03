using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Common;

namespace UKSF.Api.Services.Modpack.BuildProcess {
    public class BuildProcessHelper {
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly CancellationTokenSource errorCancellationTokenSource = new CancellationTokenSource();
        private readonly List<string> errorExclusions;
        private readonly bool errorSilently;
        private readonly AutoResetEvent errorWaitHandle = new AutoResetEvent(false);
        private readonly string ignoreErrorGateClose;
        private readonly string ignoreErrorGateOpen;
        private readonly IStepLogger logger;
        private readonly AutoResetEvent outputWaitHandle = new AutoResetEvent(false);
        private readonly bool raiseErrors;
        private readonly List<string> results = new List<string>();
        private readonly bool suppressOutput;
        private Exception capturedException;
        private bool ignoreErrors;
        private Process process;

        public BuildProcessHelper(
            IStepLogger logger,
            CancellationTokenSource cancellationTokenSource,
            bool suppressOutput = false,
            bool raiseErrors = true,
            bool errorSilently = false,
            List<string> errorExclusions = null,
            string ignoreErrorGateClose = "",
            string ignoreErrorGateOpen = ""
        ) {
            this.logger = logger;
            this.cancellationTokenSource = cancellationTokenSource;
            this.suppressOutput = suppressOutput;
            this.raiseErrors = raiseErrors;
            this.errorSilently = errorSilently;
            this.errorExclusions = errorExclusions;
            this.ignoreErrorGateClose = ignoreErrorGateClose;
            this.ignoreErrorGateOpen = ignoreErrorGateOpen;
        }

        public async Task<List<string>> Run(string workingDirectory, string executable, string args, int timeout) {
            process = new Process {
                StartInfo = {
                    FileName = executable,
                    WorkingDirectory = workingDirectory,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = false
            };

            process.OutputDataReceived += OnOutputDataReceived;
            process.ErrorDataReceived += OnErrorDataReceived;

            await using CancellationTokenRegistration unused = cancellationTokenSource.Token.Register(process.Kill);
            await using CancellationTokenRegistration _ = errorCancellationTokenSource.Token.Register(process.Kill);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (process.WaitForExit(timeout) && outputWaitHandle.WaitOne(timeout) && errorWaitHandle.WaitOne(timeout)) {
                if (capturedException != null) {
                    if (raiseErrors) {
                        throw capturedException;
                    }

                    if (!errorSilently) {
                        logger.LogError(capturedException);
                    }
                }

                if (raiseErrors && process.ExitCode != 0) {
                    string json = "";
                    List<Tuple<string, string>> messages = ExtractMessages(results.Last(), ref json);
                    if (messages.Any()) {
                        throw new Exception(messages.First().Item1);
                    }

                    throw new Exception();
                }
            } else {
                // Timeout or cancelled
                process.Kill();

                if (!cancellationTokenSource.IsCancellationRequested) {
                    Exception exception = new Exception($"Process exited with non-zero code ({process.ExitCode})");
                    if (raiseErrors) {
                        throw exception;
                    }

                    if (!errorSilently) {
                        logger.LogError(exception);
                    }
                }
            }

            return results;
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs receivedEventArgs) {
            if (receivedEventArgs.Data == null) {
                outputWaitHandle.Set();
                return;
            }

            string message = receivedEventArgs.Data;
            if (!string.IsNullOrEmpty(message)) {
                results.Add(message);
            }

            if (!suppressOutput) {
                string json = "";
                try {
                    List<Tuple<string, string>> messages = ExtractMessages(message, ref json);
                    foreach ((string text, string colour) in messages) {
                        logger.Log(text, colour);
                    }
                } catch (Exception exception) {
                    capturedException = new Exception($"Json failed: {json}\n\n{exception}");
                    errorCancellationTokenSource.Cancel();
                }
            }
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs receivedEventArgs) {
            if (receivedEventArgs.Data == null) {
                errorWaitHandle.Set();
                return;
            }

            string message = receivedEventArgs.Data;
            if (string.IsNullOrEmpty(message) || CheckIgnoreErrorGates(message)) return;

            if (errorExclusions != null && errorExclusions.Any(x => message.ContainsIgnoreCase(x))) return;

            capturedException = new Exception(message);
            errorCancellationTokenSource.Cancel();
        }

        private bool CheckIgnoreErrorGates(string message) {
            if (message.ContainsIgnoreCase(ignoreErrorGateClose)) {
                ignoreErrors = false;
                return true;
            }

            if (ignoreErrors) return true;

            if (message.ContainsIgnoreCase(ignoreErrorGateOpen)) {
                ignoreErrors = true;
                return true;
            }

            return false;
        }

        private static List<Tuple<string, string>> ExtractMessages(string message, ref string json) {
            List<Tuple<string, string>> messages = new List<Tuple<string, string>>();
            if (message.Length > 5 && message.Substring(0, 4) == "JSON") {
                string[] parts = message.Split('{', '}'); // covers cases where buffer gets extra data flushed to it after the closing brace
                json = $"{{{parts[1].Escape().Replace("\\\\n", "\\n")}}}";
                JObject jsonObject = JObject.Parse(json);
                messages.Add(new Tuple<string, string>(jsonObject.GetValueFromBody("message"), jsonObject.GetValueFromBody("colour")));
                messages.AddRange(parts.Skip(2).Where(x => !string.IsNullOrEmpty(x)).Select(extra => new Tuple<string, string>(extra, "")));
            } else {
                messages.Add(new Tuple<string, string>(message, ""));
            }

            return messages;
        }

        // public static async Task<List<string>> RunProcess(
        //     IStepLogger logger,
        //     CancellationTokenSource cancellationTokenSource,
        //     string workingDirectory,
        //     string executable,
        //     string args,
        //     double timeout,
        //     bool suppressOutput = false,
        //     bool raiseErrors = true,
        //     bool errorSilently = false,
        //     List<string> errorExclusions = null
        // ) {
        //     using Process process = new Process {
        //         StartInfo = {
        //             FileName = executable,
        //             WorkingDirectory = workingDirectory,
        //             Arguments = args,
        //             UseShellExecute = false,
        //             CreateNoWindow = true,
        //             RedirectStandardOutput = true,
        //             RedirectStandardError = true
        //         },
        //         EnableRaisingEvents = true
        //     };
        //
        //     Exception capturedException = null;
        //     CancellationTokenSource errorCancellationTokenSource = new CancellationTokenSource();
        //
        //     // TODO: Try with WaitForExit and ouput counter instead of tasks. Handle timeout?
        //
        //     TaskCompletionSource<object> processExitEvent = new TaskCompletionSource<object>();
        //     process.Exited += (sender, receivedEventArgs) => {
        //         // ((Process) sender)?.WaitForExit();
        //         processExitEvent.TrySetResult(true);
        //     };
        //     processTasks.Add(processExitEvent.Task);
        //
        //     List<string> results = new List<string>();
        //     TaskCompletionSource<bool> outputCloseEvent = new TaskCompletionSource<bool>();
        //     process.OutputDataReceived += (sender, receivedEventArgs) => {
        //         if (receivedEventArgs.Data == null) {
        //             outputCloseEvent.TrySetResult(true);
        //             return;
        //         }
        //
        //         string message = receivedEventArgs.Data;
        //         if (!string.IsNullOrEmpty(message)) {
        //             results.Add(message);
        //         }
        //
        //         if (!suppressOutput) {
        //             string json = "";
        //             try {
        //                 List<Tuple<string, string>> messages = ExtractMessages(message, ref json);
        //                 foreach ((string text, string colour) in messages) {
        //                     logger.Log(text, colour);
        //                 }
        //             } catch (Exception exception) {
        //                 capturedException = new Exception($"Json failed: {json}\n\n{exception}");
        //                 errorCancellationTokenSource.Cancel();
        //             }
        //         }
        //     };
        //     processTasks.Add(outputCloseEvent.Task);
        //
        //     bool ignoreErrors = false;
        //     TaskCompletionSource<bool> errorCloseEvent = new TaskCompletionSource<bool>();
        //     process.ErrorDataReceived += (sender, receivedEventArgs) => {
        //         if (receivedEventArgs.Data == null) {
        //             errorCloseEvent.TrySetResult(true);
        //             return;
        //         }
        //
        //         string message = receivedEventArgs.Data;
        //         if (string.IsNullOrEmpty(message)) return;
        //
        //         // TODO: Handle this better
        //         if (message.ContainsIgnoreCase("File written to")) {
        //             ignoreErrors = false;
        //             return;
        //         }
        //
        //         if (ignoreErrors) return;
        //
        //         if (message.ContainsIgnoreCase("MakePbo Version")) {
        //             ignoreErrors = true;
        //             return;
        //         }
        //
        //         if (errorExclusions != null && errorExclusions.Any(x => message.ContainsIgnoreCase(x))) return;
        //
        //         capturedException = new Exception(message);
        //         errorCancellationTokenSource.Cancel();
        //     };
        //     processTasks.Add(errorCloseEvent.Task);
        //
        //     await using CancellationTokenRegistration unused = cancellationTokenSource.Token.Register(process.Kill);
        //     await using CancellationTokenRegistration _ = errorCancellationTokenSource.Token.Register(process.Kill);
        //
        //     Task processCompletionTask = Task.WhenAll(processTasks);
        //     Task<Task> awaitingTask = Task.WhenAny(Task.Delay((int) timeout, cancellationTokenSource.Token), processCompletionTask);
        //     process.Start();
        //     process.BeginOutputReadLine();
        //     process.BeginErrorReadLine();
        //     if (await awaitingTask.ConfigureAwait(false) == processCompletionTask) {
        //         if (capturedException != null) {
        //             if (raiseErrors) {
        //                 throw capturedException;
        //             }
        //
        //             if (!errorSilently) {
        //                 logger.LogError(capturedException);
        //             }
        //         }
        //
        //         if (raiseErrors && process.ExitCode != 0) {
        //             string json = "";
        //             List<Tuple<string, string>> messages = ExtractMessages(results.Last(), ref json);
        //             if (messages.Any()) {
        //                 throw new Exception(messages.First().Item1);
        //             }
        //
        //             throw new Exception();
        //         }
        //     } else {
        //         process.Kill();
        //
        //         if (!cancellationTokenSource.IsCancellationRequested) {
        //             Exception exception = new Exception($"Process exited with non-zero code ({process.ExitCode})");
        //             if (raiseErrors) {
        //                 throw exception;
        //             }
        //
        //             if (!errorSilently) {
        //                 logger.LogError(exception);
        //             }
        //         }
        //     }
        //
        //     return results;
        // }
    }
}
