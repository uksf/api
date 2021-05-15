using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace UKSF.Api.Shared.Services
{
    public interface IHttpContextService
    {
        bool IsUserAuthenticated();
        public string GetUserId();
        public string GetUserEmail();
        bool UserHasPermission(string permission);
    }

    public class HttpContextService : IHttpContextService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpContextService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public bool IsUserAuthenticated()
        {
            return _httpContextAccessor.HttpContext?.User.Identity != null && _httpContextAccessor.HttpContext.User.Identity.IsAuthenticated;
        }

        public string GetUserId()
        {
            return _httpContextAccessor.HttpContext?.User.Claims.SingleOrDefault(x => x.Type == ClaimTypes.Sid)?.Value;
        }

        public string GetUserEmail()
        {
            return _httpContextAccessor.HttpContext?.User.Claims.SingleOrDefault(x => x.Type == ClaimTypes.Email)?.Value;
        }

        public bool UserHasPermission(string permission)
        {
            return _httpContextAccessor.HttpContext != null && _httpContextAccessor.HttpContext.User.Claims.Any(x => x.Type == ClaimTypes.Role && x.Value == permission);
        }
    }
}
