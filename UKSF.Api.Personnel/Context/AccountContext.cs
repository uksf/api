using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Context;

namespace UKSF.Api.Personnel.Context {
    public interface IAccountContext : IMongoContext<Account>, ICachedMongoContext { }

    public class AccountContext : CachedMongoContext<Account>, IAccountContext {
        public AccountContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(mongoCollectionFactory, eventBus, "accounts") { }
    }
}
