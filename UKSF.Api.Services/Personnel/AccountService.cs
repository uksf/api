using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Personnel;

namespace UKSF.Api.Services.Personnel {
    public class AccountService : DataBackedService<IAccountDataService>, IAccountService {
        public AccountService(IAccountDataService data) : base(data) { }
    }
}
