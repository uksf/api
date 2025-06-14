using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Commands;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Services;
using UKSF.Api.Exceptions;
using UKSF.Api.Models.Request;
using UKSF.Api.Models.Response;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("auth")]
public class AuthController(
    ILoginService loginService,
    IHttpContextService httpContextService,
    IRequestPasswordResetCommand requestPasswordResetCommand,
    IResetPasswordCommand resetPasswordCommand
) : ControllerBase
{
    [HttpGet]
    public bool IsUserAuthenticated()
    {
        return httpContextService.IsUserAuthenticated();
    }

    [HttpGet("refresh")]
    [Authorize]
    public TokenResponse RefreshToken()
    {
        var loginToken = loginService.RegenerateBearerToken();
        if (loginToken == null)
        {
            throw new TokenRefreshFailedException("Failed to refresh token");
        }

        return loginToken;
    }

    [HttpPost("login")]
    public TokenResponse Login([FromBody] LoginCredentials credentials)
    {
        if (string.IsNullOrEmpty(credentials.Email) || string.IsNullOrEmpty(credentials.Password))
        {
            throw new BadRequestException();
        }

        return loginService.Login(credentials.Email, credentials.Password);
    }

    [HttpPost("passwordReset")]
    public async Task RequestPasswordReset([FromBody] RequestPasswordReset requestPasswordReset)
    {
        await requestPasswordResetCommand.ExecuteAsync(new RequestPasswordResetCommandArgs(requestPasswordReset.Email));
    }

    [HttpPost("passwordReset/{code}")]
    public async Task<TokenResponse> ResetPassword([FromRoute] string code, [FromBody] LoginCredentials credentials)
    {
        await resetPasswordCommand.ExecuteAsync(new ResetPasswordCommandArgs(credentials.Email, credentials.Password, code));
        return loginService.LoginForPasswordReset(credentials.Email);
    }

    [HttpGet("impersonate")]
    [Authorize]
    [Permissions(Permissions.Superadmin)]
    public TokenResponse Impersonate([FromQuery] string accountId)
    {
        return loginService.LoginForImpersonate(accountId);
    }
}
