using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Auth.Commands;
using UKSF.Api.Auth.Exceptions;
using UKSF.Api.Auth.Models.Parameters;
using UKSF.Api.Auth.Services;
using UKSF.Api.Shared.Exceptions;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Auth.Controllers
{
    [Route("auth")]
    public class AuthController : Controller
    {
        private readonly IHttpContextService _httpContextService;
        private readonly ILoginService _loginService;
        private readonly IRequestPasswordResetCommand _requestPasswordResetCommand;
        private readonly IResetPasswordCommand _resetPasswordCommand;

        public AuthController(ILoginService loginService, IHttpContextService httpContextService, IRequestPasswordResetCommand requestPasswordResetCommand, IResetPasswordCommand resetPasswordCommand)
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

        [HttpGet("refresh"), Authorize]
        public string RefreshToken()
        {
            string loginToken = _loginService.RegenerateBearerToken(_httpContextService.GetUserId());
            if (loginToken == null)
            {
                throw new TokenRefreshFailedException();
            }

            return loginToken;
        }

        [HttpPost("login")]
        public string Login([FromBody] LoginCredentials credentials)
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
            await _requestPasswordResetCommand.ExecuteAsync(new(requestPasswordReset.Email));
        }

        [HttpPost("passwordReset/{code}")]
        public async Task<string> ResetPassword([FromRoute] string code, [FromBody] LoginCredentials credentials)
        {
            await _resetPasswordCommand.ExecuteAsync(new(credentials.Email, credentials.Password, code));
            return _loginService.LoginForPasswordReset(credentials.Email);
        }
    }
}
