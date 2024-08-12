using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface INotificationsContext : IMongoContext<Notification>, ICachedMongoContext;

public class NotificationsContext : CachedMongoContext<Notification>, INotificationsContext
{
    public NotificationsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "notifications"
    ) { }
}
