using System.Security.Claims;

namespace UKSF.Api.Core.Services;

public interface IHttpContextService
{
    bool IsUserAuthenticated();
    string GetImpersonatingUserId();
    bool HasImpersonationExpired();
    string GetUserId();
    string GetUserEmail();
    string GetUserDisplayName(bool withRank = false);
    bool UserHasPermission(string permission);
    void SetContextId(string id);
}

public class HttpContextService(IHttpContextAccessor httpContextAccessor, IClock clock, IDisplayNameService displayNameService) : IHttpContextService
{
    public bool IsUserAuthenticated()
    {
        return httpContextAccessor.HttpContext?.User.Identity is not null && httpContextAccessor.HttpContext.User.Identity.IsAuthenticated;
    }

    public string GetImpersonatingUserId()
    {
        return httpContextAccessor.HttpContext?.User.Claims.SingleOrDefault(x => x.Type == UksfClaimTypes.ImpersonatingUserId)?.Value;
    }

    public bool HasImpersonationExpired()
    {
        if (string.IsNullOrEmpty(GetImpersonatingUserId()))
        {
            return false;
        }

        var expiry = httpContextAccessor.HttpContext?.User.Claims.SingleOrDefault(x => x.Type == UksfClaimTypes.Expiry)?.Value;
        if (string.IsNullOrEmpty(expiry))
        {
            throw new Exception("Token has no expiry");
        }

        return clock.UtcNow() > DateTimeOffset.FromUnixTimeSeconds(long.Parse(expiry)).UtcDateTime;
    }

    public string GetUserId()
    {
        return httpContextAccessor.HttpContext?.User.Claims.SingleOrDefault(x => x.Type == ClaimTypes.Sid)?.Value;
    }

    public string GetUserEmail()
    {
        return httpContextAccessor.HttpContext?.User.Claims.SingleOrDefault(x => x.Type == ClaimTypes.Email)?.Value;
    }

    public string GetUserDisplayName(bool withRank = false)
    {
        var userId = GetUserId();
        return withRank ? displayNameService.GetDisplayName(userId) : displayNameService.GetDisplayNameWithoutRank(userId);
    }

    public bool UserHasPermission(string permission)
    {
        return httpContextAccessor.HttpContext is not null &&
               httpContextAccessor.HttpContext.User.Claims.Any(x => x.Type == ClaimTypes.Role && x.Value == permission);
    }

    public void SetContextId(string id)
    {
        var currentId = httpContextAccessor.HttpContext?.User.Claims.SingleOrDefault(x => x.Type == ClaimTypes.Sid)?.Value;
        if (string.IsNullOrEmpty(currentId) || currentId == id)
        {
            httpContextAccessor.HttpContext =
                new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim> { new(ClaimTypes.Sid, id) })) };
            return;
        }

        throw new Exception($"Tried to overwrite user ID ({currentId})");
    }
}

