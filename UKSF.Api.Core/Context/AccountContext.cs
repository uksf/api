using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Context;

public interface IAccountContext : IMongoContext<DomainAccount>, ICachedMongoContext { }

public class AccountContext : CachedMongoContext<DomainAccount>, IAccountContext
{
    public Guid Id;

    public AccountContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "accounts")
    {
        Id = Guid.NewGuid();
    }
}
