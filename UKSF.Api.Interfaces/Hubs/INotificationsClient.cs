using System.Collections.Generic;
using System.Threading.Tasks;

namespace UKSF.Api.Interfaces.Hubs {
    public interface INotificationsClient {
        Task ReceiveNotification(object notification);
        Task ReceiveRead(IEnumerable<string> ids);
        Task ReceiveClear(IEnumerable<string> ids);
    }
}
