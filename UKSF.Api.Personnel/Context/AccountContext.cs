using UKSF.Api.Base.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Personnel.Context {
    public interface IAccountContext : IMongoContext<Account>, ICachedMongoContext { }

    public class AccountContext : CachedMongoContext<Account>, IAccountContext {
        public AccountContext(IMongoCollectionFactory mongoCollectionFactory, IDataEventBus<Account> dataEventBus) : base(mongoCollectionFactory, dataEventBus, "accounts") { }
    }
}
