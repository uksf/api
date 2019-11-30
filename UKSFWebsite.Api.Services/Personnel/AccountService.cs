using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Personnel;

namespace UKSFWebsite.Api.Services.Personnel {
    public class AccountService : DataBackedService<IAccountDataService>, IAccountService {
        public AccountService(IAccountDataService data) : base(data) { }
    }
}
