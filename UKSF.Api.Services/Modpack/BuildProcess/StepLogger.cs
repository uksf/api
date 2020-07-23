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
            LogLines($"\nFinished: {buildStep.name}", "green");
            await logCallback();
        }

        public async Task LogCancelled() {
            LogLines("\nBuild cancelled", "goldenrod");
            await logCallback();
        }

        public async Task LogSkipped() {
            LogLines($"\nSkipped: {buildStep.name}", "orange");
            await logCallback();
        }

        public async Task LogWarning(string message) {
            LogLines($"Warning\n{message}", "orange");
            await logCallback();
        }

        public async Task LogError(Exception exception) {
            LogLines($"Error\n{exception.Message}\n{exception.StackTrace}\n\nFailed: {buildStep.name}", "red");
            await logCallback();
        }

        public async Task Log(string log, string colour = "") {
            LogLines(log, colour);
            await logCallback();
        }

        private void LogLines(string log, string colour = "") {
            foreach (string line in log.Split("\n")) {
                buildStep.logs.Add(new ModpackBuildStepLogItem {text = line, colour = string.IsNullOrEmpty(line) ? "" : colour});
            }
        }
    }
}
