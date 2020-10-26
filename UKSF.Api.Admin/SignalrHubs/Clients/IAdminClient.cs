using System.Threading.Tasks;
using UKSF.Api.Logging.Models;

namespace UKSF.Api.Admin.SignalrHubs.Clients {
    public interface IAdminClient {
        Task ReceiveAuditLog(AuditLogMessage log);
        Task ReceiveErrorLog(WebLogMessage log);
        Task ReceiveLauncherLog(LauncherLogMessage log);
        Task ReceiveLog(BasicLogMessage log);
    }
}
