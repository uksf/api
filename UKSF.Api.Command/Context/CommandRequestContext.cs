using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Command.Models;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.Command.Context;

public interface ICommandRequestContext : IMongoContext<CommandRequest>, ICachedMongoContext { }

public class CommandRequestContext : CachedMongoContext<CommandRequest>, ICommandRequestContext
{
    public CommandRequestContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) :
        base(mongoCollectionFactory, eventBus, "commandRequests") { }
}
