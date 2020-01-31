using System;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Message;

namespace UKSF.Api.Data.Message {
    public class NotificationsDataService : CachedDataService<Notification, INotificationsDataService>, INotificationsDataService {
        private readonly IDataCollection dataCollection;
        
        public NotificationsDataService(IDataCollection dataCollection, IDataEventBus<INotificationsDataService> dataEventBus) : base(dataCollection, dataEventBus, "notifications") => this.dataCollection = dataCollection;

        public async Task UpdateMany(Func<Notification, bool> predicate, UpdateDefinition<Notification> update) {
            await dataCollection.UpdateMany(x => predicate(x), update);
            Refresh();
        }

        public async Task DeleteMany(Func<Notification, bool> predicate) {
            await dataCollection.DeleteMany<Notification>(x => predicate(x));
            Refresh();
        }
    }
}
