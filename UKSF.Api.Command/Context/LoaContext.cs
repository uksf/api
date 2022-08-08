using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Command.Models;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.Command.Context;

public interface ILoaContext : IMongoContext<DomainLoa>, ICachedMongoContext { }

public class LoaContext : CachedMongoContext<DomainLoa>, ILoaContext
{
    public LoaContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "loas") { }
}
