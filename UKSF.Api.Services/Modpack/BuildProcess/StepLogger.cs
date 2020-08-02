using System;
using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Services.Modpack.BuildProcess {
    public class StepLogger : IStepLogger {
        private readonly ModpackBuildStep buildStep;

        public StepLogger(ModpackBuildStep buildStep) => this.buildStep = buildStep;

        public void LogStart() {
            LogLines($"Starting: {buildStep.name}", string.Empty);
        }

        public void LogSuccess() {
            LogLines(
                $"\nFinished{(buildStep.buildResult == ModpackBuildResult.WARNING ? " with warning" : "")}: {buildStep.name}",
                buildStep.buildResult == ModpackBuildResult.WARNING ? "orangered" : "green"
            );
        }

        public void LogCancelled() {
            LogLines("\nBuild cancelled", "goldenrod");
        }

        public void LogSkipped() {
            LogLines($"\nSkipped: {buildStep.name}", "gray");
        }

        public void LogWarning(string message) {
            LogLines($"Warning\n{message}", "orangered");
        }

        public void LogError(Exception exception) {
            LogLines($"Error\n{exception.Message}\n{exception.StackTrace}\n\nFailed: {buildStep.name}", "red");
        }

        public void LogSurround(string log) {
            LogLines(log, "cadetblue");
        }

        public void Log(string log, string colour = "") {
            LogLines(log, colour);
        }

        public void LogInline(string log) {
            PushLogUpdate(new List<ModpackBuildStepLogItem> { new ModpackBuildStepLogItem { text = log } }, true);
        }

        private void LogLines(string log, string colour = "") {
            List<ModpackBuildStepLogItem> logs = log.Split("\n").Select(x => new ModpackBuildStepLogItem { text = x, colour = string.IsNullOrEmpty(x) ? "" : colour }).ToList();
            if (logs.Count == 0) return;

            PushLogUpdate(logs);
        }

        private void PushLogUpdate(IEnumerable<ModpackBuildStepLogItem> logs, bool inline = false) {
            if (inline) {
                buildStep.logs[^1] = logs.First();
            } else {
                buildStep.logs.AddRange(logs);
            }
        }
    }
}
