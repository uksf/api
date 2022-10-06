using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using UKSF.Api.Extensions;
using UKSF.Api.Models;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Exceptions;
using UKSF.Api.Shared.Models;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Services;

public interface ILoginService
{
    TokenResponse Login(string email, string password);
    TokenResponse LoginForPasswordReset(string email);
    TokenResponse LoginForImpersonate(string accountId);
    TokenResponse RegenerateBearerToken();
}

public class LoginService : ILoginService
{
    private readonly IAccountContext _accountContext;
    private readonly IHttpContextService _httpContextService;
    private readonly IPermissionsService _permissionsService;

    public LoginService(IAccountContext accountContext, IPermissionsService permissionsService, IHttpContextService httpContextService)
    {
        _accountContext = accountContext;
        _permissionsService = permissionsService;
        _httpContextService = httpContextService;
    }

    public TokenResponse Login(string email, string password)
    {
        var domainAccount = AuthenticateAccount(email, password);
        return GenerateBearerToken(domainAccount);
    }

    public TokenResponse LoginForPasswordReset(string email)
    {
        var domainAccount = AuthenticateAccount(email, "", true);
        return GenerateBearerToken(domainAccount);
    }

    public TokenResponse LoginForImpersonate(string accountId)
    {
        var domainAccount = _accountContext.GetSingle(accountId);
        if (domainAccount == null)
        {
            throw new BadRequestException($"No user found with id {accountId}");
        }

        return GenerateBearerToken(domainAccount, true);
    }

    public TokenResponse RegenerateBearerToken()
    {
        var domainAccount = _accountContext.GetSingle(_httpContextService.GetUserId());
        if (domainAccount == null)
        {
            throw new BadRequestException("No user found with that email");
        }

        if (_httpContextService.HasImpersonationExpired())
        {
            throw new TokenRefreshFailedException("Impersonation session expired");
        }

        return GenerateBearerToken(domainAccount);
    }

    private DomainAccount AuthenticateAccount(string email, string password, bool passwordReset = false)
    {
        var domainAccount = _accountContext.GetSingle(x => string.Equals(x.Email, email, StringComparison.InvariantCultureIgnoreCase));
        if (domainAccount == null)
        {
            throw new BadRequestException("No user found with that email");
        }

        if (passwordReset)
        {
            return domainAccount;
        }

        if (!BCrypt.Net.BCrypt.Verify(password, domainAccount.Password))
        {
            throw new BadRequestException("Password or email did not match");
        }

        return domainAccount;
    }

    private TokenResponse GenerateBearerToken(DomainAccount domainAccount, bool impersonating = false)
    {
        List<Claim> claims = new()
        {
            new(ClaimTypes.Email, domainAccount.Email, ClaimValueTypes.String), new(ClaimTypes.Sid, domainAccount.Id, ClaimValueTypes.String)
        };
        claims.AddRange(_permissionsService.GrantPermissions(domainAccount).Select(x => new Claim(ClaimTypes.Role, x)));

        if (impersonating)
        {
            claims.Add(new(UksfClaimTypes.ImpersonatingUserId, _httpContextService.GetUserId()));
        }
        else
        {
            var impersonatingUserId = _httpContextService.GetImpersonatingUserId();
            if (!string.IsNullOrEmpty(impersonatingUserId))
            {
                impersonating = true;
                claims.Add(new(UksfClaimTypes.ImpersonatingUserId, impersonatingUserId));
            }
        }

        var token = new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(
                AuthExtensions.TokenIssuer,
                AuthExtensions.TokenAudience,
                claims,
                DateTime.UtcNow,
                impersonating ? DateTime.UtcNow.AddMinutes(15) : DateTime.UtcNow.AddDays(15),
                new(AuthExtensions.SecurityKey, SecurityAlgorithms.HmacSha256)
            )
        );

        return new() { Token = token };
    }
}
