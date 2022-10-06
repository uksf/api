using System.Security.Claims;

namespace UKSF.Api.Shared.Services;

public interface IHttpContextService
{
    bool IsUserAuthenticated();
    string GetImpersonatingUserId();
    bool HasImpersonationExpired();
    public string GetUserId();
    public string GetUserEmail();
    bool UserHasPermission(string permission);
}

public class HttpContextService : IHttpContextService
{
    private readonly IClock _clock;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextService(IHttpContextAccessor httpContextAccessor, IClock clock)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
    }

    public bool IsUserAuthenticated()
    {
        return _httpContextAccessor.HttpContext?.User.Identity != null && _httpContextAccessor.HttpContext.User.Identity.IsAuthenticated;
    }

    public string GetImpersonatingUserId()
    {
        return _httpContextAccessor.HttpContext?.User.Claims.SingleOrDefault(x => x.Type == UksfClaimTypes.ImpersonatingUserId)?.Value;
    }

    public bool HasImpersonationExpired()
    {
        if (string.IsNullOrEmpty(GetImpersonatingUserId()))
        {
            return false;
        }

        var expiry = _httpContextAccessor.HttpContext?.User.Claims.SingleOrDefault(x => x.Type == UksfClaimTypes.Expiry)?.Value;
        if (string.IsNullOrEmpty(expiry))
        {
            throw new("Token has no expiry");
        }

        return _clock.UtcNow() > DateTimeOffset.FromUnixTimeSeconds(long.Parse(expiry)).UtcDateTime;
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
        return _httpContextAccessor.HttpContext != null &&
               _httpContextAccessor.HttpContext.User.Claims.Any(x => x.Type == ClaimTypes.Role && x.Value == permission);
    }
}
