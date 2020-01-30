using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Message;

namespace UKSF.Api.Data.Message {
    public class NotificationsDataService : CachedDataService<Notification, INotificationsDataService>, INotificationsDataService {
        public NotificationsDataService(IMongoDatabase database, IDataEventBus<INotificationsDataService> dataEventBus) : base(database, dataEventBus, "notifications") { }

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
