using UKSF.Api.Base.Database;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Services.Data;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Services.Data {
    public interface IAccountDataService : IDataService<Account>, ICachedDataService { }

    public class AccountDataService : CachedDataService<Account>, IAccountDataService {
        public AccountDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<Account> dataEventBus) : base(dataCollectionFactory, dataEventBus, "accounts") { }
    }
}
