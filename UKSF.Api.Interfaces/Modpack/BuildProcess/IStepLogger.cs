using System;
using System.Threading.Tasks;

namespace UKSF.Api.Interfaces.Modpack.BuildProcess {
    public interface IStepLogger {
        Task LogStart();
        Task LogSuccess();
        Task LogCancelled();
        Task LogSkipped();
        Task LogWarning(string message);
        Task LogError(Exception exception);
        Task LogSurround(string log);
        Task LogInline(string log);
        Task Log(string log, string colour = "");
    }
}
