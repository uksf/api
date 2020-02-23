using System;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Message;

namespace UKSF.Api.Data.Message {
    public class NotificationsDataService : CachedDataService<Notification, INotificationsDataService>, INotificationsDataService {
        public NotificationsDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<INotificationsDataService> dataEventBus) : base(dataCollectionFactory, dataEventBus, "notifications") { }
    }
}
