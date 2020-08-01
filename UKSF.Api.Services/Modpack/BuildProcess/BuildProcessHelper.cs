using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Common;

namespace UKSF.Api.Services.Modpack.BuildProcess {
    public static class BuildProcessHelper {
        public static string RunProcess(
            IStepLogger logger,
            CancellationToken cancellationToken,
            string workingDirectory,
            string executable,
            string args,
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

            try {
                process.EnableRaisingEvents = false;
                string result = "";
                List<Exception> exceptions = new List<Exception>();

                process.OutputDataReceived += (sender, receivedEventArgs) => {
                    if (receivedEventArgs.Data == null) return;

                    string message = receivedEventArgs.Data;
                    if (!string.IsNullOrEmpty(message)) {
                        result = message;
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
                            exceptions.Add(new Exception($"Json failed: {json}\n\n{exception}"));
                        }
                    }
                };

                process.ErrorDataReceived += (sender, receivedEventArgs) => {
                    if (receivedEventArgs.Data == null) return;

                    string message = receivedEventArgs.Data;
                    if (string.IsNullOrEmpty(message)) return;

                    if (errorExclusions != null && errorExclusions.All(x => !message.ContainsIgnoreCase(x))) return;

                    Exception exception = new Exception(message);
                    exceptions.Add(exception);
                };

                using CancellationTokenRegistration unused = cancellationToken.Register(process.Kill);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (exceptions.Any()) {
                    if (raiseErrors) {
                        throw exceptions.First();
                    }

                    if (!errorSilently) {
                        IEnumerable<string> exceptionStrings = exceptions.Select(x => x.ToString());
                        logger.LogError(string.Join("\n\n", exceptionStrings));
                    }
                }

                return result;
            } finally {
                process.Close();
            }
        }

        [SuppressMessage("ReSharper", "MethodHasAsyncOverload")] // async runspace.OpenAsync is not as it seems
        public static async Task<PSDataCollection<PSObject>> RunPowershell(
            IStepLogger logger,
            CancellationToken cancellationToken,
            string workingDirectory,
            IEnumerable<string> commands,
            bool suppressOutput = false,
            bool raiseErrors = true,
            bool errorSilently = false,
            List<string> errorExclusions = null
        ) {
            using Runspace runspace = RunspaceFactory.CreateRunspace();
            runspace.Open();
            runspace.SessionStateProxy.Path.SetLocation(workingDirectory);

            void Log(object sender, DataAddedEventArgs eventArgs) {
                PSDataCollection<InformationRecord> streamObjectsReceived = sender as PSDataCollection<InformationRecord>;
                InformationRecord currentStreamRecord = streamObjectsReceived?[eventArgs.Index];
                logger.Log(currentStreamRecord?.MessageData.ToString());
            }

            void Verbose(object sender, DataAddedEventArgs eventArgs) {
                PSDataCollection<VerboseRecord> streamObjectsReceived = sender as PSDataCollection<VerboseRecord>;
                VerboseRecord currentStreamRecord = streamObjectsReceived?[eventArgs.Index];
                logger.Log(currentStreamRecord?.Message);
            }

            void ProgressLog(object sender, DataAddedEventArgs eventArgs) {
                PSDataCollection<ProgressRecord> streamObjectsReceived = sender as PSDataCollection<ProgressRecord>;
                ProgressRecord currentStreamRecord = streamObjectsReceived?[eventArgs.Index];
                logger.Log(currentStreamRecord?.PercentComplete.ToString());
            }

            void Warning(object sender, DataAddedEventArgs eventArgs) {
                PSDataCollection<WarningRecord> streamObjectsReceived = sender as PSDataCollection<WarningRecord>;
                WarningRecord currentStreamRecord = streamObjectsReceived?[eventArgs.Index];
                logger.LogWarning(currentStreamRecord?.Message);
            }

            using PowerShell powerShell = PowerShell.Create(runspace);

            if (!suppressOutput) {
                powerShell.Streams.Information.DataAdded += Log;
                powerShell.Streams.Verbose.DataAdded += Verbose;
                powerShell.Streams.Progress.DataAdded += ProgressLog;
            }

            powerShell.Streams.Warning.DataAdded += Warning;

            foreach (string command in commands) {
                powerShell.AddScript(command);
            }

            PSDataCollection<PSObject> result = await powerShell.InvokeAsync(cancellationToken);
            List<Exception> exceptions = powerShell.Streams.Error.Select(x => x.Exception).ToList();
            if (errorExclusions != null) {
                exceptions = exceptions.Where(x => errorExclusions.All(y => !x.Message.ContainsIgnoreCase(y))).ToList();
            }

            if (!suppressOutput) {
                LogPowershellResult(logger, result);
            }

            if (exceptions.Any()) {
                if (raiseErrors) {
                    if (suppressOutput) {
                        LogPowershellResult(logger, result);
                    }

                    runspace.Close();
                    throw exceptions.First();
                }

                if (!errorSilently) {
                    IEnumerable<string> exceptionStrings = exceptions.Select(x => x.ToString());
                    logger.LogError(string.Join("\n\n", exceptionStrings));
                }
            }

            runspace.Close();
            return result;
        }

        private static Task<PSDataCollection<PSObject>> InvokeAsync(this PowerShell powerShell, CancellationToken cancellationToken) {
            return Task.Factory.StartNew(
                () => {
                    IAsyncResult invocation = powerShell.BeginInvoke();
                    WaitHandle.WaitAny(new[] { invocation.AsyncWaitHandle, cancellationToken.WaitHandle });

                    if (cancellationToken.IsCancellationRequested) {
                        powerShell.Stop();
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    return powerShell.EndInvoke(invocation);
                },
                cancellationToken
            );
        }

        private static void LogPowershellResult(IStepLogger logger, IEnumerable<PSObject> result) {
            IEnumerable<string> resultStrings = result.Select(x => x.BaseObject.ToString());
            logger.Log(string.Join("\n", resultStrings));
        }
    }
}
