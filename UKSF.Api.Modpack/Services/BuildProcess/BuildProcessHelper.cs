﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using UKSF.Api.Base.Extensions;

namespace UKSF.Api.Modpack.Services.BuildProcess {
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

        public List<string> Run(string workingDirectory, string executable, string args, int timeout) {
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

            using CancellationTokenRegistration unused = cancellationTokenSource.Token.Register(process.Kill);
            using CancellationTokenRegistration _ = errorCancellationTokenSource.Token.Register(process.Kill);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (process.WaitForExit(timeout) && outputWaitHandle.WaitOne(timeout) && errorWaitHandle.WaitOne(timeout)) {
                if (cancellationTokenSource.IsCancellationRequested) {
                    return results;
                }

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
    }
}