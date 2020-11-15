using System.Threading.Tasks;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Admin.Signalr.Clients {
    public interface IAdminClient {
        Task ReceiveAuditLog(AuditLog log);
        Task ReceiveErrorLog(HttpErrorLog log);
        Task ReceiveLauncherLog(LauncherLog log);
        Task ReceiveLog(BasicLog log);
    }
}
