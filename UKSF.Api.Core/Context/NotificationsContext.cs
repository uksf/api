using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface INotificationsContext : IMongoContext<DomainNotification>, ICachedMongoContext;

public class NotificationsContext : CachedMongoContext<DomainNotification>, INotificationsContext
{
    public NotificationsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "notifications"
    ) { }
}
