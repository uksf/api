using System.Security.Claims;
using UKSF.Api.Base.Services;

namespace UKSF.Api.Auth.Services {
    public interface ISessionService {
        Account GetUserAccount();
    }

    public class SessionService : ISessionService {
        private readonly IHttpContextService httpContextService;
        private readonly IAccountService accountService;

        public SessionService(IHttpContextService httpContextService, IAccountService accountService) {
            this.httpContextService = httpContextService;
            this.accountService = accountService;
        }

        public Account GetUserAccount() => accountService.Data.GetSingle(httpContextService.GetUserId());
    }
}
