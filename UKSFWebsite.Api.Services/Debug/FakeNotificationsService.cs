using System.Collections.Generic;
using System.Threading.Tasks;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services.Debug {
    public class FakeNotificationsService : FakeCachedDataService<Notification>, INotificationsService {
        public void SendTeamspeakNotification(Account account, string rawMessage) { }

        public void SendTeamspeakNotification(IEnumerable<string> clientDbIds, string rawMessage) { }

        public IEnumerable<Notification> GetNotificationsForContext() => new List<Notification>();

        public new void Add(Notification notification) { }

        public Task MarkNotificationsAsRead(IEnumerable<string> ids) => Task.CompletedTask;

        public Task Delete(IEnumerable<string> ids) => Task.CompletedTask;
    }
}
