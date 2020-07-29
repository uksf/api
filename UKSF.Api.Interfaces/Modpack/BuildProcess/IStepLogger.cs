using System;

namespace UKSF.Api.Interfaces.Modpack.BuildProcess {
    public interface IStepLogger {
        void LogStart();
        void LogSuccess();
        void LogCancelled();
        void LogSkipped();
        void LogWarning(string message);
        void LogError(Exception exception);
        void LogError(string message);
        void LogSurround(string log);
        void LogInline(string log);
        void Log(string log, string colour = "");
        void LogInstant(string log, string colour = "");
        void LogInlineInstant(string log);
    }
}
