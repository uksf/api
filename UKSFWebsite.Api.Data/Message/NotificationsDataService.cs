using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Message;

namespace UKSFWebsite.Api.Data.Message {
    public class NotificationsDataService : CachedDataService<Notification>, INotificationsDataService {
        public NotificationsDataService(IMongoDatabase database, IDataEventBus dataEventBus) : base(database, dataEventBus, "notifications") { }

        public async Task UpdateMany(FilterDefinition<Notification> filter, UpdateDefinition<Notification> update) {
            await Database.GetCollection<Notification>(DatabaseCollection).UpdateManyAsync(filter, update);
            Refresh();
        }

        public async Task DeleteMany(FilterDefinition<Notification> filter) {
            await Database.GetCollection<Notification>(DatabaseCollection).DeleteManyAsync(filter);
            Refresh();
        }
    }
}
