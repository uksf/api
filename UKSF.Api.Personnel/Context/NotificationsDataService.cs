using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Context {
    public interface INotificationsDataService : IDataService<Notification>, ICachedDataService { }

    public class NotificationsDataService : CachedDataService<Notification>, INotificationsDataService {
        public NotificationsDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<Notification> dataEventBus) : base(dataCollectionFactory, dataEventBus, "notifications") { }
    }
}
