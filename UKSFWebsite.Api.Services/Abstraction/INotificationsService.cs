using System.Collections.Generic;
using System.Threading.Tasks;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface INotificationsService : IDataService<Notification> {
        void SendTeamspeakNotification(Account account, string rawMessage);
        void SendTeamspeakNotification(IEnumerable<string> clientDbIds, string rawMessage);
        IEnumerable<Notification> GetNotificationsForContext();
        new void Add(Notification notification);
        Task MarkNotificationsAsRead(IEnumerable<string> ids);
        Task Delete(IEnumerable<string> ids);
    }
}
