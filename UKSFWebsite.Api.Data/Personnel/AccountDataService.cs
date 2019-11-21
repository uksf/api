using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Data.Personnel {
    public class AccountDataService : CachedDataService<Account, IAccountDataService>, IAccountDataService {
        public AccountDataService(IMongoDatabase database, IDataEventBus<IAccountDataService> dataEventBus) : base(database, dataEventBus, "accounts") { }
    }
}
