using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context;

public interface IAccountContext : IMongoContext<DomainAccount>, ICachedMongoContext { }

public class AccountContext : CachedMongoContext<DomainAccount>, IAccountContext
{
    public Guid Id;

    public AccountContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "accounts")
    {
        Id = Guid.NewGuid();
    }
}
