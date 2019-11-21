using System.Collections.Generic;
using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Message;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Interfaces.Message {
    public interface INotificationsService : IDataBackedService<INotificationsDataService> {
        void Add(Notification notification);
        Task SendTeamspeakNotification(Account account, string rawMessage);
        Task SendTeamspeakNotification(IEnumerable<double> clientDbIds, string rawMessage);
        IEnumerable<Notification> GetNotificationsForContext();
        Task MarkNotificationsAsRead(IEnumerable<string> ids);
        Task Delete(IEnumerable<string> ids);
    }
}
