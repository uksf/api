using System.Collections.Generic;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Models.Message;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Services.Fake {
    public class FakeNotificationsService : INotificationsService {
        public Task SendTeamspeakNotification(Account account, string rawMessage) => Task.CompletedTask;

        public Task SendTeamspeakNotification(IEnumerable<double> clientDbIds, string rawMessage) => Task.CompletedTask;

        public IEnumerable<Notification> GetNotificationsForContext() => new List<Notification>();

        public INotificationsDataService Data() => null;

        public void Add(Notification notification) { }

        public Task MarkNotificationsAsRead(IEnumerable<string> ids) => Task.CompletedTask;

        public Task Delete(IEnumerable<string> ids) => Task.CompletedTask;
    }
}
