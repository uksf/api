using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Message.Logging;

namespace UKSFWebsite.Api.Interfaces.Data {
    public interface ILogDataService : IDataService<BasicLogMessage, ILogDataService> {
        Task Add(AuditLogMessage log);
        Task Add(LauncherLogMessage log);
        Task Add(WebLogMessage log);
    }
}
