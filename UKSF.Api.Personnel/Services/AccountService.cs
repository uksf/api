using UKSF.Api.Base.Context;
using UKSF.Api.Base.Services;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Services {
    public interface IAccountService : IDataBackedService<IAccountDataService> {
        Account GetUserAccount();
    }

    public class AccountService : DataBackedService<IAccountDataService>, IAccountService {
        private readonly IHttpContextService httpContextService;

        public AccountService(IAccountDataService data, IHttpContextService httpContextService) : base(data) => this.httpContextService = httpContextService;

        public Account GetUserAccount() => Data.GetSingle(httpContextService.GetUserId());
    }
}
