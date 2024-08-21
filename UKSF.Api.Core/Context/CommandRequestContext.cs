using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface ICommandRequestContext : IMongoContext<DomainCommandRequest>, ICachedMongoContext;

public class CommandRequestContext : CachedMongoContext<DomainCommandRequest>, ICommandRequestContext
{
    public CommandRequestContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "commandRequests"
    ) { }
}
