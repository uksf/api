using System.Collections.Generic;
using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Message;
using UKSFWebsite.Api.Models.Message;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Services.Debug {
    public class FakeNotificationsService : INotificationsService {
        public void SendTeamspeakNotification(Account account, string rawMessage) { }

        public void SendTeamspeakNotification(IEnumerable<string> clientDbIds, string rawMessage) { }

        public IEnumerable<Notification> GetNotificationsForContext() => new List<Notification>();

        public INotificationsDataService Data() => null;

        public void Add(Notification notification) { }

        public Task MarkNotificationsAsRead(IEnumerable<string> ids) => Task.CompletedTask;

        public Task Delete(IEnumerable<string> ids) => Task.CompletedTask;
    }
}
