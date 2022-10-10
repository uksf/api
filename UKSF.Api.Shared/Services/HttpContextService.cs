using System.Security.Claims;

namespace UKSF.Api.Shared.Services;

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

public class HttpContextService : IHttpContextService
{
    private readonly IClock _clock;
    private readonly IDisplayNameService _displayNameService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextService(IHttpContextAccessor httpContextAccessor, IClock clock, IDisplayNameService displayNameService)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        _displayNameService = displayNameService;
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

    public string GetUserDisplayName(bool withRank = false)
    {
        var userId = GetUserId();
        return withRank ? _displayNameService.GetDisplayName(userId) : _displayNameService.GetDisplayNameWithoutRank(userId);
    }

    public bool UserHasPermission(string permission)
    {
        return _httpContextAccessor.HttpContext != null &&
               _httpContextAccessor.HttpContext.User.Claims.Any(x => x.Type == ClaimTypes.Role && x.Value == permission);
    }

    public void SetContextId(string id)
    {
        var currentId = _httpContextAccessor.HttpContext?.User.Claims.SingleOrDefault(x => x.Type == ClaimTypes.Sid)?.Value;
        if (string.IsNullOrEmpty(currentId) || currentId == id)
        {
            _httpContextAccessor.HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(new List<Claim> { new(ClaimTypes.Sid, id) })) };
            return;
        }

        throw new($"Tried to overwrite user ID ({currentId})");
    }
}

