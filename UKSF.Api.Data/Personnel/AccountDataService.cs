using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Data.Personnel {
    public class AccountDataService : CachedDataService<Account>, IAccountDataService {
        public AccountDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<Account> dataEventBus) : base(dataCollectionFactory, dataEventBus, "accounts") { }
    }
}
