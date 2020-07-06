using System;
using System.Threading.Tasks;

namespace UKSF.Api.Interfaces.Modpack.BuildProcess {
    public interface IStepLogger {
        Task LogStart();
        Task LogSuccess();
        Task LogError(Exception exception);
        Task LogCancelled();
        Task Log(string log);
    }
}
