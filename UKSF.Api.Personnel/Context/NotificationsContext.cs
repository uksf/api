using UKSF.Api.Base.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Personnel.Context {
    public interface INotificationsContext : IMongoContext<Notification>, ICachedMongoContext { }

    public class NotificationsContext : CachedMongoContext<Notification>, INotificationsContext {
        public NotificationsContext(IMongoCollectionFactory mongoCollectionFactory, IDataEventBus<Notification> dataEventBus) : base(mongoCollectionFactory, dataEventBus, "notifications") { }
    }
}
