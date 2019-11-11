using System.Collections.Generic;
using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Message;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Interfaces.Message {
    public interface INotificationsService {
        INotificationsDataService Data();
        void Add(Notification notification);
        void SendTeamspeakNotification(Account account, string rawMessage);
        void SendTeamspeakNotification(IEnumerable<string> clientDbIds, string rawMessage);
        IEnumerable<Notification> GetNotificationsForContext();
        Task MarkNotificationsAsRead(IEnumerable<string> ids);
        Task Delete(IEnumerable<string> ids);
    }
}
