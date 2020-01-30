using System.Threading.Tasks;
using UKSF.Api.Models.Message.Logging;

namespace UKSF.Api.Interfaces.Data {
    public interface ILogDataService : IDataService<BasicLogMessage, ILogDataService> {
        Task Add(AuditLogMessage log);
        Task Add(LauncherLogMessage log);
        Task Add(WebLogMessage log);
    }
}
