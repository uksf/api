using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UKSF.Api.Auth.Services;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Auth.Controllers
{
    [Route("[controller]")]
    public class LoginController : Controller
    {
        private readonly IHttpContextService _httpContextService;
        private readonly ILoginService _loginService;

        public LoginController(ILoginService loginService, IHttpContextService httpContextService)
        {
            _loginService = loginService;
            _httpContextService = httpContextService;
        }

        [HttpGet]
        public bool IsUserAuthenticated()
        {
            return _httpContextService.IsUserAuthenticated();
        }

        [HttpGet("refresh"), Authorize]
        public IActionResult RefreshToken()
        {
            string loginToken = _loginService.RegenerateBearerToken(_httpContextService.GetUserId());
            return loginToken != null ? Ok(loginToken) : BadRequest();
        }

        [HttpPost]
        public IActionResult Login([FromBody] JObject body)
        {
            string email = body.GetValueFromBody("email");
            string password = body.GetValueFromBody("password");

            try
            {
                GuardUtilites.ValidateString(email, _ => throw new ArgumentException("Email is invalid. Please try again"));
                GuardUtilites.ValidateString(password, _ => throw new ArgumentException("Password is invalid. Please try again"));
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { error = exception.Message });
            }

            try
            {
                return Ok(_loginService.Login(email, password));
            }
            catch (LoginFailedException e)
            {
                return BadRequest(new { message = e.Message });
            }
        }
    }
}
