using System;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Services.Modpack.BuildProcess {
    public class StepLogger : IStepLogger {
        private const int LOG_COUNT_MAX = 10;
        private readonly ModpackBuildStep buildStep;
        private readonly object lockObject = new object();
        private readonly Action logEvent;
        private readonly Func<Task> updateCallback;
        private int logCount;

        public StepLogger(ModpackBuildStep buildStep, Func<Task> updateCallback, Action logEvent) {
            this.buildStep = buildStep;
            this.updateCallback = updateCallback;
            this.logEvent = logEvent;
        }

        public void LogStart() {
            LogLines($"Starting: {buildStep.name}");
            FlushLogsInstantly();
        }

        public void LogSuccess() {
            LogLines(
                $"\nFinished{(buildStep.buildResult == ModpackBuildResult.WARNING ? " with warning" : "")}: {buildStep.name}",
                buildStep.buildResult == ModpackBuildResult.WARNING ? "orangered" : "green"
            );
            FlushLogsInstantly();
        }

        public void LogCancelled() {
            LogLines("\nBuild cancelled", "goldenrod");
            FlushLogsInstantly();
        }

        public void LogSkipped() {
            LogLines($"\nSkipped: {buildStep.name}", "orangered");
            FlushLogsInstantly();
        }

        public void LogWarning(string message) {
            LogLines($"Warning\n{message}", "orangered");
            FlushLogsInstantly();
        }

        public void LogError(Exception exception) {
            LogLines($"Error\n{exception.Message}\n{exception.StackTrace}\n\nFailed: {buildStep.name}", "red");
            FlushLogsInstantly();
        }

        public void LogSurround(string log) {
            LogLines(log, "cadetblue");
        }

        public void LogInline(string log) {
            lock (lockObject) {
                buildStep.logs[^1] = new ModpackBuildStepLogItem { text = log };
            }

            IncrementCountAndFlushLogs();
        }

        public void Log(string log, string colour = "") {
            LogLines(log, colour);
        }

        public void LogInstant(string log, string colour = "") {
            LogLines(log, colour);
            FlushLogsInstantly();
        }

        public void LogInlineInstant(string log) {
            LogInline(log);
            FlushLogsInstantly(false);
        }

        public void FlushLogs(bool force = false, bool synchronous = false) {
            if (force || logCount > LOG_COUNT_MAX) {
                logCount = 0;
                Task callback = updateCallback();
                if (synchronous) {
                    callback.Wait();
                }
            }
        }

        private void FlushLogsInstantly(bool synchronous = true) {
            FlushLogs(true, synchronous);
        }

        private void LogLines(string log, string colour = "") {
            lock (lockObject) {
                foreach (string line in log.Split("\n")) {
                    buildStep.logs.Add(new ModpackBuildStepLogItem { text = line, colour = string.IsNullOrEmpty(line) ? "" : colour });
                }
            }

            logEvent();
            IncrementCountAndFlushLogs();
        }

        private void IncrementCountAndFlushLogs() {
            Interlocked.Increment(ref logCount);
            FlushLogs();
        }
    }
}
