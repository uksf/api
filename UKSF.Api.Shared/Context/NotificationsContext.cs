using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context;

public interface INotificationsContext : IMongoContext<Notification>, ICachedMongoContext { }

public class NotificationsContext : CachedMongoContext<Notification>, INotificationsContext
{
    public NotificationsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) :
        base(mongoCollectionFactory, eventBus, "notifications") { }
}
