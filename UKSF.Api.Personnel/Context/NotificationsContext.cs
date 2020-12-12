using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.Personnel.Context {
    public interface INotificationsContext : IMongoContext<Notification>, ICachedMongoContext { }

    public class NotificationsContext : CachedMongoContext<Notification>, INotificationsContext {
        public NotificationsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "notifications") { }
    }
}
