﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Common;

namespace UKSF.Api.Services.Modpack.BuildProcess {
    public static class BuildProcessHelper {
        public static string RunProcess(IStepLogger logger, bool raiseErrors, CancellationToken cancellationToken, string executable, string workingDirectory, string args) {
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
                process.OutputDataReceived += (sender, receivedEventArgs) => {
                    result = receivedEventArgs.Data;
                    logger.Log(result);
                };
                process.ErrorDataReceived += (sender, receivedEventArgs) => {
                    Exception exception = new Exception(receivedEventArgs.Data);
                    if (raiseErrors) {
                        throw exception;
                    }

                    logger.LogError(exception);
                };
                using CancellationTokenRegistration unused = cancellationToken.Register(process.Kill);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0) {
                    throw new Exception($"Process exited with non-zero exit code of: {process.ExitCode}");
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
            List<string> commands,
            bool suppressOutput = false,
            bool raiseErrors = true,
            bool errorSilently = false,
            List<string> errorExclusions = null
        ) {
            using Runspace runspace = RunspaceFactory.CreateRunspace();
            runspace.Open();
            runspace.SessionStateProxy.Path.SetLocation(workingDirectory);

            using PowerShell powerShell = PowerShell.Create(runspace);
            foreach (string command in commands) {
                powerShell.AddScript(command);
            }

            void Log(object sender, DataAddedEventArgs eventArgs) {
                PSDataCollection<InformationRecord> streamObjectsReceived = sender as PSDataCollection<InformationRecord>;
                InformationRecord currentStreamRecord = streamObjectsReceived?[eventArgs.Index];
                logger.Log(currentStreamRecord?.MessageData.ToString());
            }

            List<Exception> exceptions = new List<Exception>();

            void Error(object sender, DataAddedEventArgs eventArgs) {
                PSDataCollection<ErrorRecord> streamObjectsReceived = sender as PSDataCollection<ErrorRecord>;
                ErrorRecord currentStreamRecord = streamObjectsReceived?[eventArgs.Index];
                exceptions.Add(currentStreamRecord?.Exception);
            }

            if (!suppressOutput) {
                powerShell.Streams.Information.DataAdded += Log;
                powerShell.Streams.Warning.DataAdded += Log;
            }

            powerShell.Streams.Error.DataAdded += Error;

            PSDataCollection<PSObject> result = await powerShell.InvokeAsync(cancellationToken);
            if (errorExclusions != null) {
                exceptions = exceptions.Where(x => errorExclusions.All(y => !x.Message.ContainsIgnoreCase(y))).ToList();
            }

            if (exceptions.Count > 0) {
                if (raiseErrors) {
                    LogPowershellResult(logger, result);
                    runspace.Close();
                    throw exceptions.First();
                }

                if (!suppressOutput) {
                    LogPowershellResult(logger, result);
                }

                if (!errorSilently) {
                    IEnumerable<string> exceptionStrings = exceptions.Select(x => x.ToString());
                    logger.LogError(string.Join("\n\n", exceptionStrings));
                }
            }

            if (!suppressOutput) {
                LogPowershellResult(logger, result);
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