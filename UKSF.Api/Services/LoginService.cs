using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Exceptions;
using UKSF.Api.Extensions;
using UKSF.Api.Models.Response;

namespace UKSF.Api.Services;

public interface ILoginService
{
    TokenResponse Login(string email, string password);
    TokenResponse LoginForPasswordReset(string email);
    TokenResponse LoginForImpersonate(string accountId);
    TokenResponse RegenerateBearerToken();
}

public class LoginService(IAccountContext accountContext, IPermissionsService permissionsService, IHttpContextService httpContextService) : ILoginService
{
    public TokenResponse Login(string email, string password)
    {
        var account = AuthenticateAccount(email, password);
        return GenerateBearerToken(account);
    }

    public TokenResponse LoginForPasswordReset(string email)
    {
        var account = AuthenticateAccount(email, "", true);
        return GenerateBearerToken(account);
    }

    public TokenResponse LoginForImpersonate(string accountId)
    {
        var account = accountContext.GetSingle(accountId);
        if (account == null)
        {
            throw new BadRequestException($"No user found with id {accountId}");
        }

        return GenerateBearerToken(account, true);
    }

    public TokenResponse RegenerateBearerToken()
    {
        var account = accountContext.GetSingle(httpContextService.GetUserId());
        if (account == null)
        {
            throw new BadRequestException("No user found with that email");
        }

        if (httpContextService.HasImpersonationExpired())
        {
            throw new TokenRefreshFailedException("Impersonation session expired");
        }

        return GenerateBearerToken(account);
    }

    private DomainAccount AuthenticateAccount(string email, string password, bool passwordReset = false)
    {
        var account = accountContext.GetSingle(x => string.Equals(x.Email, email, StringComparison.InvariantCultureIgnoreCase));
        if (account == null)
        {
            throw new BadRequestException("No user found with that email");
        }

        if (passwordReset)
        {
            return account;
        }

        if (!BCrypt.Net.BCrypt.Verify(password, account.Password))
        {
            throw new BadRequestException("Password or email did not match");
        }

        return account;
    }

    private TokenResponse GenerateBearerToken(DomainAccount account, bool impersonating = false)
    {
        List<Claim> claims = [new(ClaimTypes.Email, account.Email, ClaimValueTypes.String), new(ClaimTypes.Sid, account.Id, ClaimValueTypes.String)];
        claims.AddRange(permissionsService.GrantPermissions(account).Select(x => new Claim(ClaimTypes.Role, x)));

        if (impersonating)
        {
            claims.Add(new Claim(UksfClaimTypes.ImpersonatingUserId, httpContextService.GetUserId()));
        }
        else
        {
            var impersonatingUserId = httpContextService.GetImpersonatingUserId();
            if (!string.IsNullOrEmpty(impersonatingUserId))
            {
                impersonating = true;
                claims.Add(new Claim(UksfClaimTypes.ImpersonatingUserId, impersonatingUserId));
            }
        }

        var token = new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(
                AuthExtensions.TokenIssuer,
                AuthExtensions.TokenAudience,
                claims,
                DateTime.UtcNow,
                impersonating ? DateTime.UtcNow.AddMinutes(15) : DateTime.UtcNow.AddDays(15),
                new SigningCredentials(AuthExtensions.SecurityKey, SecurityAlgorithms.HmacSha256)
            )
        );

        return new TokenResponse { Token = token };
    }
}
