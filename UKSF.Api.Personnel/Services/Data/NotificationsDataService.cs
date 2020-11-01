using UKSF.Api.Base.Database;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Services.Data;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Services.Data {
    public interface INotificationsDataService : IDataService<Notification>, ICachedDataService { }

    public class NotificationsDataService : CachedDataService<Notification>, INotificationsDataService {
        public NotificationsDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<Notification> dataEventBus) : base(dataCollectionFactory, dataEventBus, "notifications") { }
    }
}
