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
    public static class BuildProcessHelper {
        public static async Task<List<string>> RunProcess(
            IStepLogger logger,
            CancellationTokenSource cancellationTokenSource,
            string workingDirectory,
            string executable,
            string args,
            double timeout,
            bool suppressOutput = false,
            bool raiseErrors = true,
            bool errorSilently = false,
            List<string> errorExclusions = null
        ) {
                using Process process = new Process {
                    StartInfo = {
                        FileName = executable,
                        WorkingDirectory = workingDirectory,
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                List<Task> processTasks = new List<Task>();
                process.EnableRaisingEvents = true;
                Exception capturedException = null;
                CancellationTokenSource errorCancellationTokenSource = new CancellationTokenSource();

                TaskCompletionSource<object> processExitEvent = new TaskCompletionSource<object>();
                process.Exited += (sender, receivedEventArgs) => {
                    ((Process)sender)?.WaitForExit();
                    processExitEvent.TrySetResult(true);
                };
                processTasks.Add(processExitEvent.Task);

                List<string> results = new List<string>();
                TaskCompletionSource<bool> outputCloseEvent = new TaskCompletionSource<bool>();
                process.OutputDataReceived += (sender, receivedEventArgs) => {
                    if (receivedEventArgs.Data == null) {
                        outputCloseEvent.TrySetResult(true);
                        return;
                    }

                    string message = receivedEventArgs.Data;
                    if (!string.IsNullOrEmpty(message)) {
                        results.Add(message);
                    }

                    if (!suppressOutput) {
                        string json = "";
                        try {
                            if (message.Length > 5 && message.Substring(0, 4) == "JSON") {
                                json = message.Replace("JSON:", "").Escape().Replace("\\\\n", "\\n");
                                JObject jsonObject = JObject.Parse(json);
                                logger.Log(jsonObject.GetValueFromBody("message"), jsonObject.GetValueFromBody("colour"));
                            } else {
                                logger.Log(message);
                            }
                        } catch (Exception exception) {
                            capturedException = new Exception($"Json failed: {json}\n\n{exception}");
                            errorCancellationTokenSource.Cancel();
                        }
                    }
                };
                processTasks.Add(outputCloseEvent.Task);

                TaskCompletionSource<bool> errorCloseEvent = new TaskCompletionSource<bool>();
                process.ErrorDataReceived += (sender, receivedEventArgs) => {
                    if (receivedEventArgs.Data == null) {
                        errorCloseEvent.TrySetResult(true);
                        return;
                    }

                    string message = receivedEventArgs.Data;
                    if (string.IsNullOrEmpty(message)) return;

                    if (errorExclusions != null && errorExclusions.All(x => !message.ContainsIgnoreCase(x))) return;

                    capturedException = new Exception(message);
                    errorCancellationTokenSource.Cancel();
                };
                processTasks.Add(errorCloseEvent.Task);

                await using CancellationTokenRegistration unused = cancellationTokenSource.Token.Register(process.Kill);
                await using CancellationTokenRegistration _ = errorCancellationTokenSource.Token.Register(process.Kill);

                Task processCompletionTask = Task.WhenAll(processTasks);
                Task<Task> awaitingTask = Task.WhenAny(Task.Delay((int) timeout, cancellationTokenSource.Token), processCompletionTask);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (await awaitingTask.ConfigureAwait(false) == processCompletionTask) {
                    if (capturedException != null) {
                        if (raiseErrors) {
                            throw capturedException;
                        }

                        if (!errorSilently) {
                            logger.LogError(capturedException);
                        }
                    }
                } else {
                    logger.LogWarning("Process error");
                    try {
                        process.Kill();
                    } catch {
                        logger.LogWarning("Process timed out");
                    }
                }

                return results;
        }
    }
}
