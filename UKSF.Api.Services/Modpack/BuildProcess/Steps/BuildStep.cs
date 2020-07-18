using System;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Interfaces.Modpack.BuildProcess.Steps;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps {
    public class BuildStep : IBuildStep {
        protected ModpackBuild Build;
        private ModpackBuildStep buildStep;
        protected CancellationTokenSource CancellationTokenSource;
        protected IStepLogger Logger;
        private Func<Task> updateCallback;

        public void Init(ModpackBuild modpackBuild, ModpackBuildStep modpackBuildStep, Func<Task> stepUpdateCallback, CancellationTokenSource newCancellationTokenSource) {
            Build = modpackBuild;
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
            await Logger.LogSuccess();
            buildStep.buildResult = ModpackBuildResult.SUCCESS;
            await Stop();
        }

        public async Task Fail(Exception exception) {
            await Logger.LogError(exception);
            buildStep.buildResult = ModpackBuildResult.FAILED;
            await Stop();
        }

        public async Task Cancel() {
            await Logger.LogCancelled();
            buildStep.buildResult = ModpackBuildResult.CANCELLED;
            await Stop();
        }

        private async Task LogCallback() {
            await updateCallback();
        }

        private async Task Stop() {
            buildStep.running = false;
            buildStep.finished = true;
            buildStep.endTime = DateTime.Now;
            await updateCallback();
        }
    }
}
