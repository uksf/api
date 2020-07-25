using System;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Interfaces.Modpack.BuildProcess.Steps;
using UKSF.Api.Models.Game;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Services.Admin;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps {
    public class BuildStep : IBuildStep {
        protected const string COLOUR_BLUE = "#0c78ff";

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

        public virtual Task<bool> CheckGuards() => Task.FromResult(true);

        public async Task Setup() {
            CancellationTokenSource.Token.ThrowIfCancellationRequested();
            await Logger.Log("\nSetup", COLOUR_BLUE);
            await SetupExecute();
        }

        public async Task Process() {
            CancellationTokenSource.Token.ThrowIfCancellationRequested();
            await Logger.Log("\nProcess", COLOUR_BLUE);
            await ProcessExecute();
        }

        public async Task Teardown() {
            CancellationTokenSource.Token.ThrowIfCancellationRequested();
            await Logger.Log("\nTeardown", COLOUR_BLUE);
            await TeardownExecute();
        }

        public async Task Succeed() {
            await Logger.LogSuccess();
            if (buildStep.buildResult != ModpackBuildResult.WARNING) {
                buildStep.buildResult = ModpackBuildResult.SUCCESS;
            }

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

        public async Task Warning(string message) {
            await Logger.LogWarning(message);
            buildStep.buildResult = ModpackBuildResult.WARNING;
        }

        public async Task Skip() {
            await Logger.LogSkipped();
            buildStep.buildResult = ModpackBuildResult.SKIPPED;
            await Stop();
        }

        protected virtual async Task SetupExecute() {
            await Logger.Log("---");
        }

        protected virtual async Task ProcessExecute() {
            await Logger.Log("---");
        }

        protected virtual async Task TeardownExecute() {
            await Logger.Log("---");
        }

        protected async Task<bool> ReleaseBuildGuard() {
            if (Build.environment != GameEnvironment.RELEASE) {
                await Warning("\nBuild is not a release build, but the definition contains a release step.\nThis is a configuration error, please notify an admin.");
                return false;
            }

            return true;
        }

        internal string GetBuildEnvironmentPath() => GetEnvironmentPath(Build.environment);

        internal static string GetEnvironmentPath(GameEnvironment environment) =>
            environment switch {
                GameEnvironment.RELEASE => VariablesWrapper.VariablesDataService().GetSingle("MODPACK_PATH_RELEASE").AsString(),
                GameEnvironment.RC => VariablesWrapper.VariablesDataService().GetSingle("MODPACK_PATH_RC").AsString(),
                GameEnvironment.DEV => VariablesWrapper.VariablesDataService().GetSingle("MODPACK_PATH_DEV").AsString(),
                _ => throw new ArgumentException("Invalid build environment")
            };

        internal static string GetServerEnvironmentPath(GameEnvironment environment) =>
            environment switch {
                GameEnvironment.RELEASE => VariablesWrapper.VariablesDataService().GetSingle("SERVER_PATH_RELEASE").AsString(),
                GameEnvironment.RC => VariablesWrapper.VariablesDataService().GetSingle("SERVER_PATH_RC").AsString(),
                GameEnvironment.DEV => VariablesWrapper.VariablesDataService().GetSingle("SERVER_PATH_DEV").AsString(),
                _ => throw new ArgumentException("Invalid build environment")
            };

        internal string GetEnvironmentRepoName() =>
            Build.environment switch {
                GameEnvironment.RELEASE => "UKSF",
                GameEnvironment.RC => "UKSF-Rc",
                GameEnvironment.DEV => "UKSF-Dev",
                _ => throw new ArgumentException("Invalid build environment")
            };

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
