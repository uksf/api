using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Message;

namespace UKSFWebsite.Api.Services.Debug {
    public class FakeNotificationsDataService : FakeCachedDataService<Notification>, INotificationsDataService {
        public Task UpdateMany(FilterDefinition<Notification> filter, UpdateDefinition<Notification> update) => Task.CompletedTask;

        public Task DeleteMany(FilterDefinition<Notification> filter) => Task.CompletedTask;
    }
}