using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Interfaces.Utility;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Services.Utility {
    public class SessionService : ISessionService {
        private readonly IAccountService accountService;
        private readonly IHttpContextAccessor httpContext;

        public SessionService(IHttpContextAccessor httpContext, IAccountService accountService) {
            this.httpContext = httpContext;
            this.accountService = accountService;
        }

        public Account GetContextAccount() => accountService.Data().GetSingle(GetContextId());

        public string GetContextId() {
            return httpContext.HttpContext.User.Claims.Single(x => x.Type == ClaimTypes.Sid).Value;
        }

        public string GetContextEmail() {
            return httpContext.HttpContext.User.Claims.Single(x => x.Type == ClaimTypes.Email).Value;
        }

        public bool ContextHasRole(string role) {
            return httpContext.HttpContext.User.Claims.Any(x => x.Type == ClaimTypes.Role && x.Value == role);
        }
    }
}
