using MongoDB.Driver;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Data.Personnel {
    public class AccountDataService : CachedDataService<Account, IAccountDataService>, IAccountDataService {
        public AccountDataService(IMongoDatabase database, IDataEventBus<IAccountDataService> dataEventBus) : base(database, dataEventBus, "accounts") { }
    }
}
