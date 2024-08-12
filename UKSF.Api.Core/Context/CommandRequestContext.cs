using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface ICommandRequestContext : IMongoContext<CommandRequest>, ICachedMongoContext;

public class CommandRequestContext : CachedMongoContext<CommandRequest>, ICommandRequestContext
{
    public CommandRequestContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "commandRequests"
    ) { }
}
