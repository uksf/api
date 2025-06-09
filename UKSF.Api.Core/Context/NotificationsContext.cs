using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface INotificationsContext : IMongoContext<DomainNotification>, ICachedMongoContext;

public class NotificationsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<DomainNotification>(mongoCollectionFactory, eventBus, variablesService, "notifications"), INotificationsContext;
