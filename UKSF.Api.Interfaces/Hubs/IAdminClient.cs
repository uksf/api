using System.Threading.Tasks;
using UKSF.Api.Models.Message.Logging;

namespace UKSF.Api.Interfaces.Hubs {
    public interface IAdminClient {
        Task ReceiveAuditLog(AuditLogMessage log);
        Task ReceiveErrorLog(WebLogMessage log);
        Task ReceiveLauncherLog(LauncherLogMessage log);
        Task ReceiveLog(BasicLogMessage log);
    }
}
