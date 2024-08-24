using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface ICommandRequestContext : IMongoContext<DomainCommandRequest>, ICachedMongoContext;

public class CommandRequestContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<DomainCommandRequest>(mongoCollectionFactory, eventBus, variablesService, "commandRequests"), ICommandRequestContext;
