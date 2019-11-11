using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Personnel;

namespace UKSFWebsite.Api.Services.Personnel {
    public class AccountService : IAccountService {
        private readonly IAccountDataService data;

        public AccountService(IAccountDataService data) => this.data = data;

        public IAccountDataService Data() => data;
    }
}
