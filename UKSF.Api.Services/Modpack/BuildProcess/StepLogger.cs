using System;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Services.Modpack.BuildProcess {
    public class StepLogger : IStepLogger {
        private readonly ModpackBuildStep buildStep;
        private readonly Func<Task> logCallback;

        public StepLogger(ModpackBuildStep buildStep, Func<Task> logCallback) {
            this.buildStep = buildStep;
            this.logCallback = logCallback;
        }

        public async Task LogStart() {
            LogLines($"Starting: {buildStep.name}");
            await logCallback();
        }

        public async Task LogSuccess() {
            LogLines($"\nFinished: {buildStep.name}");
            await logCallback();
        }

        public async Task LogError(Exception exception) {
            LogLines($"\nError: {exception.Message}\n{exception.StackTrace}");
            LogLines($"\nFailed: {buildStep.name}");
            await logCallback();
        }

        public async Task LogCancelled() {
            LogLines("\nBuild cancelled");
            await logCallback();
        }

        public async Task Log(string log) {
            LogLines(log);
            await logCallback();
        }

        private void LogLines(string log) {
            foreach (string line in log.Split("\n")) {
                buildStep.logs.Add(line);
            }
        }
    }
}
