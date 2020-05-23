using System.Collections.Generic;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Models.Message;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Services.Fake {
    public class FakeNotificationsService : DataBackedService<INotificationsDataService>, INotificationsService {
        public FakeNotificationsService(INotificationsDataService data) : base(data) { }

        public Task SendTeamspeakNotification(Account account, string rawMessage) => Task.CompletedTask;

        public Task SendTeamspeakNotification(IEnumerable<double> clientDbIds, string rawMessage) => Task.CompletedTask;

        public IEnumerable<Notification> GetNotificationsForContext() => new List<Notification>();

        public void Add(Notification notification) { }

        public Task MarkNotificationsAsRead(List<string> ids) => Task.CompletedTask;

        public Task Delete(List<string> ids) => Task.CompletedTask;
    }
}
