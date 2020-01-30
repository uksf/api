using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Personnel;

namespace UKSF.Api.Services.Personnel {
    public class AccountService : IAccountService {
        private readonly IAccountDataService data;

        public AccountService(IAccountDataService data) => this.data = data;

        public IAccountDataService Data() => data;
    }
}
