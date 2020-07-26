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
        private Action logEvent;

        public void Init(ModpackBuild modpackBuild, ModpackBuildStep modpackBuildStep, Func<Task> stepUpdateCallback, Action stepLogEvent, CancellationTokenSource newCancellationTokenSource) {
            Build = modpackBuild;
            buildStep = modpackBuildStep;
            updateCallback = stepUpdateCallback;
            logEvent = stepLogEvent;
            CancellationTokenSource = newCancellationTokenSource;
            Logger = new StepLogger(buildStep, async () => await updateCallback(), logEvent);
        }

        public async Task Start() {
            CancellationTokenSource.Token.ThrowIfCancellationRequested();
            buildStep.running = true;
            buildStep.startTime = DateTime.Now;
            Logger.LogStart();
            await updateCallback();
        }

        public virtual bool CheckGuards() => true;

        public async Task Setup() {
            CancellationTokenSource.Token.ThrowIfCancellationRequested();
            Logger.Log("\nSetup", COLOUR_BLUE);
            await SetupExecute();
        }

        public async Task Process() {
            CancellationTokenSource.Token.ThrowIfCancellationRequested();
            Logger.Log("\nProcess", COLOUR_BLUE);
            await ProcessExecute();
        }

        public async Task Teardown() {
            CancellationTokenSource.Token.ThrowIfCancellationRequested();
            Logger.Log("\nTeardown", COLOUR_BLUE);
            await TeardownExecute();
        }

        public async Task Succeed() {
            Logger.LogSuccess();
            if (buildStep.buildResult != ModpackBuildResult.WARNING) {
                buildStep.buildResult = ModpackBuildResult.SUCCESS;
            }

            await Stop();
        }

        public async Task Fail(Exception exception) {
            Logger.LogError(exception);
            buildStep.buildResult = ModpackBuildResult.FAILED;
            await Stop();
        }

        public async Task Cancel() {
            Logger.LogCancelled();
            buildStep.buildResult = ModpackBuildResult.CANCELLED;
            await Stop();
        }

        public void Warning(string message) {
            Logger.LogWarning(message);
            buildStep.buildResult = ModpackBuildResult.WARNING;
        }

        public async Task Skip() {
            Logger.LogSkipped();
            buildStep.buildResult = ModpackBuildResult.SKIPPED;
            await Stop();
        }

        protected virtual Task SetupExecute() {
            Logger.Log("---");
            return Task.CompletedTask;
        }

        protected virtual Task ProcessExecute() {
            Logger.Log("---");
            return Task.CompletedTask;
        }

        protected virtual Task TeardownExecute() {
            Logger.Log("---");
            return Task.CompletedTask;
        }

        protected bool ReleaseBuildGuard() {
            if (Build.environment != GameEnvironment.RELEASE) {
                Warning("\nBuild is not a release build, but the definition contains a release step.\nThis is a configuration error, please notify an admin.");
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

        private async Task Stop() {
            buildStep.running = false;
            buildStep.finished = true;
            buildStep.endTime = DateTime.Now;
            await updateCallback();
        }
    }
}
