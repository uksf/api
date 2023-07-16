using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface IAccountContext : IMongoContext<DomainAccount>, ICachedMongoContext { }

public class AccountContext : CachedMongoContext<DomainAccount>, IAccountContext
{
    public AccountContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "accounts"
    )
    {
    }
}
