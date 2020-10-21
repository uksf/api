using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Accounts.Services.Auth {
    public interface ISessionService {
        Account GetUserAccount();
        string GetUserEmail();
        string GetUserId();
        bool UserHasPermission(string permission);
    }

    public class SessionService : ISessionService {
        private readonly IAccountService accountService;
        private readonly IHttpContextAccessor httpContextAccessor;

        public SessionService(IHttpContextAccessor httpContextAccessor, IAccountService accountService) {
            this.httpContextAccessor = httpContextAccessor;
            this.accountService = accountService;
        }

        public Account GetUserAccount() => accountService.Data.GetSingle(GetUserId());

        public string GetUserId() => httpContextAccessor.HttpContext?.User.Claims.Single(x => x.Type == ClaimTypes.Sid).Value;

        public string GetUserEmail() => httpContextAccessor.HttpContext?.User.Claims.Single(x => x.Type == ClaimTypes.Email).Value;

        public bool UserHasPermission(string permission) =>
            httpContextAccessor.HttpContext != null && httpContextAccessor.HttpContext.User.Claims.Any(x => x.Type == ClaimTypes.Role && x.Value == permission);
    }
}
