using System;
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Modpack.BuildProcess;

namespace UKSF.Api.Services.Modpack.BuildProcess {
    public static class BuildProcessHelper {
        [SuppressMessage("ReSharper", "MethodHasAsyncOverload")] // async runspace.OpenAsync is not as it seems
        public static async Task RunPowershell(IStepLogger logger, bool raiseErrors, CancellationToken cancellationToken, string workingDirectory, string command) {
            using Runspace runspace = RunspaceFactory.CreateRunspace();
            runspace.Open();
            runspace.SessionStateProxy.Path.SetLocation(workingDirectory);

            using PowerShell powerShell = PowerShell.Create();
            powerShell.Runspace = runspace;
            powerShell.AddScript(command);

            void Log(object sender, DataAddedEventArgs eventArgs) {
                PSDataCollection<InformationRecord> streamObjectsReceived = sender as PSDataCollection<InformationRecord>;
                InformationRecord currentStreamRecord = streamObjectsReceived?[eventArgs.Index];
                logger.Log(currentStreamRecord?.MessageData.ToString());
            }

            Exception exception = null;

            void Error(object sender, DataAddedEventArgs eventArgs) {
                PSDataCollection<ErrorRecord> streamObjectsReceived = sender as PSDataCollection<ErrorRecord>;
                ErrorRecord currentStreamRecord = streamObjectsReceived?[eventArgs.Index];
                exception = currentStreamRecord?.Exception;
            }

            powerShell.Streams.Information.DataAdded += Log;
            powerShell.Streams.Warning.DataAdded += Log;
            powerShell.Streams.Error.DataAdded += Error;

            PSDataCollection<PSObject> result = await powerShell.InvokeAsync(cancellationToken);
            if (exception != null) {
                if (raiseErrors) {
                    runspace.Close();
                    throw exception;
                }

                logger.LogError(exception);
            }

            foreach (PSObject psObject in result) {
                logger.Log(psObject.BaseObject.ToString());
            }

            runspace.Close();
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
    }
}
