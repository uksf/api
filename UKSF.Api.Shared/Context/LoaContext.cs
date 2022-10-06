using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context;

public interface ILoaContext : IMongoContext<DomainLoa>, ICachedMongoContext { }

public class LoaContext : CachedMongoContext<DomainLoa>, ILoaContext
{
    public LoaContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "loas") { }
}
