using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Commands;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Services;
using UKSF.Api.Extensions;
using UKSF.Api.Models.Request;
using UKSF.Api.Models.Response;
using UKSF.Api.Services;

namespace UKSF.Api.Controllers;

[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IHttpContextService _httpContextService;
    private readonly ILoginService _loginService;
    private readonly IRequestPasswordResetCommand _requestPasswordResetCommand;
    private readonly IResetPasswordCommand _resetPasswordCommand;

    public AuthController(
        ILoginService loginService,
        IHttpContextService httpContextService,
        IRequestPasswordResetCommand requestPasswordResetCommand,
        IResetPasswordCommand resetPasswordCommand
    )
    {
        _loginService = loginService;
        _httpContextService = httpContextService;
        _requestPasswordResetCommand = requestPasswordResetCommand;
        _resetPasswordCommand = resetPasswordCommand;
    }

    [HttpGet]
    public bool IsUserAuthenticated()
    {
        return _httpContextService.IsUserAuthenticated();
    }

    [HttpGet("refresh")]
    [Authorize]
    public TokenResponse RefreshToken()
    {
        var loginToken = _loginService.RegenerateBearerToken();
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

        return _loginService.Login(credentials.Email, credentials.Password);
    }

    [HttpPost("passwordReset")]
    public async Task RequestPasswordReset([FromBody] RequestPasswordReset requestPasswordReset)
    {
        await _requestPasswordResetCommand.ExecuteAsync(new RequestPasswordResetCommandArgs(requestPasswordReset.Email));
    }

    [HttpPost("passwordReset/{code}")]
    public async Task<TokenResponse> ResetPassword([FromRoute] string code, [FromBody] LoginCredentials credentials)
    {
        await _resetPasswordCommand.ExecuteAsync(new ResetPasswordCommandArgs(credentials.Email, credentials.Password, code));
        return _loginService.LoginForPasswordReset(credentials.Email);
    }

    [HttpGet("impersonate")]
    [Authorize]
    [Permissions(Permissions.Superadmin)]
    public TokenResponse Impersonate([FromQuery] string accountId)
    {
        return _loginService.LoginForImpersonate(accountId);
    }
}
