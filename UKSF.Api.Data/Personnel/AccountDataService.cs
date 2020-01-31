using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Data.Personnel {
    public class AccountDataService : CachedDataService<Account, IAccountDataService>, IAccountDataService {
        public AccountDataService(IDataCollection dataCollection, IDataEventBus<IAccountDataService> dataEventBus) : base(dataCollection, dataEventBus, "accounts") { }
    }
}
