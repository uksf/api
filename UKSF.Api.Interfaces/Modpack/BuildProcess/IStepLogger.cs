using System;

namespace UKSF.Api.Interfaces.Modpack.BuildProcess {
    public interface IStepLogger {
        void LogStart();
        void LogSuccess();
        void LogCancelled();
        void LogSkipped();
        void LogWarning(string message);
        void LogError(Exception exception);
        void LogSurround(string log);
        void Log(string log, string colour = "");
        void LogInline(string log);
    }
}
