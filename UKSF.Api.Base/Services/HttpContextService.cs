using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace UKSF.Api.Base.Services {
    public interface IHttpContextService {
        public string GetUserId();
        public string GetUserEmail();
        bool UserHasPermission(string permission);
    }

    public class HttpContextService : IHttpContextService {
        private readonly IHttpContextAccessor httpContextAccessor;

        public HttpContextService(IHttpContextAccessor httpContextAccessor) => this.httpContextAccessor = httpContextAccessor;

        public string GetUserId() => httpContextAccessor.HttpContext?.User.Claims.Single(x => x.Type == ClaimTypes.Sid).Value;

        public string GetUserEmail() => httpContextAccessor.HttpContext?.User.Claims.Single(x => x.Type == ClaimTypes.Email).Value;

        public bool UserHasPermission(string permission) =>
            httpContextAccessor.HttpContext != null && httpContextAccessor.HttpContext.User.Claims.Any(x => x.Type == ClaimTypes.Role && x.Value == permission);
    }
}
