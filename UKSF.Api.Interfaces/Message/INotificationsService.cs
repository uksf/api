using System.Collections.Generic;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Models.Message;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Interfaces.Message {
    public interface INotificationsService : IDataBackedService<INotificationsDataService> {
        void Add(Notification notification);
        Task SendTeamspeakNotification(Account account, string rawMessage);
        Task SendTeamspeakNotification(IEnumerable<double> clientDbIds, string rawMessage);
        IEnumerable<Notification> GetNotificationsForContext();
        Task MarkNotificationsAsRead(List<string> ids);
        Task Delete(List<string> ids);
    }
}
