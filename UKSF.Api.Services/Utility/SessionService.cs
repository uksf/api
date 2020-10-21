using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Services.Utility {
    public class SessionService : ISessionService {
        private readonly IAccountService accountService;
        private readonly IHttpContextAccessor httpContextAccessor;

        public SessionService(IHttpContextAccessor httpContextAccessor, IAccountService accountService) {
            this.httpContextAccessor = httpContextAccessor;
            this.accountService = accountService;
        }

        public Account GetContextAccount() => accountService.Data.GetSingle(GetContextId());

        public string GetContextId() {
            return httpContextAccessor.HttpContext?.User.Claims.Single(x => x.Type == ClaimTypes.Sid).Value;
        }

        public string GetContextEmail() {
            return httpContextAccessor.HttpContext?.User.Claims.Single(x => x.Type == ClaimTypes.Email).Value;
        }

        public bool ContextHasRole(string role) {
            return httpContextAccessor.HttpContext != null && httpContextAccessor.HttpContext.User.Claims.Any(x => x.Type == ClaimTypes.Role && x.Value == role);
        }
    }
}
