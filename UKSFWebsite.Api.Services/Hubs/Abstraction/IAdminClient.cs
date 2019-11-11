using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Message.Logging;

namespace UKSFWebsite.Api.Services.Hubs.Abstraction {
    public interface IAdminClient {
        Task ReceiveAuditLog(AuditLogMessage log);
        Task ReceiveErrorLog(WebLogMessage log);
        Task ReceiveLauncherLog(LauncherLogMessage log);
        Task ReceiveLog(BasicLogMessage log);
    }
}
