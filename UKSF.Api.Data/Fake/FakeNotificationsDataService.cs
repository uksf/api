using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Models.Message;
using UKSF.Api.Services.Fake;

namespace UKSF.Api.Data.Fake {
    public class FakeNotificationsDataService : FakeCachedDataService<Notification, INotificationsDataService>, INotificationsDataService {
        public Task UpdateMany(FilterDefinition<Notification> filter, UpdateDefinition<Notification> update) => Task.CompletedTask;

        public Task DeleteMany(FilterDefinition<Notification> filter) => Task.CompletedTask;
    }
}
