using System;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Interfaces.Modpack.BuildProcess.Steps;
using UKSF.Api.Models.Game;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Services.Admin;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps {
    public class BuildStep : IBuildStep {
        private const string COLOUR_BLUE = "#0c78ff";

        protected ModpackBuild Build;
        private ModpackBuildStep buildStep;
        protected CancellationTokenSource CancellationTokenSource;
        protected IStepLogger Logger;
        private Func<UpdateDefinition<ModpackBuild>, Task> updateBuildCallback;
        private Func<Task> updateStepCallback;
        private Action logEvent;

        public void Init(ModpackBuild modpackBuild, ModpackBuildStep modpackBuildStep, Func<UpdateDefinition<ModpackBuild>, Task> buildUpdateCallback, Func<Task> stepUpdateCallback, Action stepLogEvent, CancellationTokenSource newCancellationTokenSource) {
            Build = modpackBuild;
            buildStep = modpackBuildStep;
            updateBuildCallback = buildUpdateCallback;
            updateStepCallback = stepUpdateCallback;
            logEvent = stepLogEvent;
            CancellationTokenSource = newCancellationTokenSource;
            Logger = new StepLogger(buildStep, async () => await updateStepCallback(), logEvent);
        }

        public async Task Start() {
            CancellationTokenSource.Token.ThrowIfCancellationRequested();
            buildStep.running = true;
            buildStep.startTime = DateTime.Now;
            Logger.LogStart();
            await updateStepCallback();
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

        internal static string GetBuildSourcesPath() => VariablesWrapper.VariablesDataService().GetSingle("BUILD_PATH_SOURCES").AsString();

        internal void SetEnvironmentVariable(string key, object value) {
            if (Build.environmentVariables.ContainsKey(key)) {
                Build.environmentVariables[key] = value;
            } else {
                Build.environmentVariables.Add(key, value);
            }

            updateBuildCallback(Builders<ModpackBuild>.Update.Set(x => x.environmentVariables, Build.environmentVariables));
        }

        internal T GetEnvironmentVariable<T>(string key) {
            if (Build.environmentVariables.ContainsKey(key)) {
                object value = Build.environmentVariables[key];
                return (T) value;
            }

            return default;
        }

        private async Task Stop() {
            buildStep.running = false;
            buildStep.finished = true;
            buildStep.endTime = DateTime.Now;
            await updateStepCallback();
        }
    }
}
