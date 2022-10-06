using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context;

public interface ICommandRequestContext : IMongoContext<CommandRequest>, ICachedMongoContext { }

public class CommandRequestContext : CachedMongoContext<CommandRequest>, ICommandRequestContext
{
    public CommandRequestContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) :
        base(mongoCollectionFactory, eventBus, "commandRequests") { }
}
