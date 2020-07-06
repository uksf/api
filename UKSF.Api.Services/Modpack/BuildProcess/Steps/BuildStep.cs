using System;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Interfaces.Modpack.BuildProcess.Steps;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps {
    public class BuildStep : IBuildStep {
        private ModpackBuildStep buildStep;
        protected CancellationTokenSource CancellationTokenSource;
        protected IStepLogger Logger;
        private Func<Task> updateCallback;

        public void Init(ModpackBuildStep modpackBuildStep, Func<Task> stepUpdateCallback, CancellationTokenSource newCancellationTokenSource) {
            buildStep = modpackBuildStep;
            updateCallback = stepUpdateCallback;
            CancellationTokenSource = newCancellationTokenSource;
            Logger = new StepLogger(buildStep, LogCallback);
        }

        public async Task Start() {
            CancellationTokenSource.Token.ThrowIfCancellationRequested();
            buildStep.running = true;
            buildStep.startTime = DateTime.Now;
            await Logger.LogStart();
        }

        public virtual async Task Setup() {
            CancellationTokenSource.Token.ThrowIfCancellationRequested();
            await Logger.Log("\nSetup");
        }

        public virtual async Task Process() {
            CancellationTokenSource.Token.ThrowIfCancellationRequested();
            await Logger.Log("\nProcess");
        }

        public virtual async Task Teardown() {
            CancellationTokenSource.Token.ThrowIfCancellationRequested();
            await Logger.Log("\nTeardown");
        }

        public async Task Succeed() {
            Stop();
            buildStep.buildResult = ModpackBuildResult.SUCCESS;
            await Logger.LogSuccess();
        }

        public async Task Fail(Exception exception) {
            Stop();
            buildStep.buildResult = ModpackBuildResult.FAILED;
            await Logger.LogError(exception);
        }

        public async Task Cancel() {
            Stop();
            buildStep.buildResult = ModpackBuildResult.CANCELLED;
            await Logger.LogCancelled();
        }

        private async Task LogCallback() {
            await updateCallback();
        }

        private void Stop() {
            buildStep.running = false;
            buildStep.finished = true;
            buildStep.endTime = DateTime.Now;
        }
    }
}
