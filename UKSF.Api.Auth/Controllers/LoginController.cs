using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UKSF.Api.Auth.Services;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Auth.Controllers {
    [Route("[controller]")]
    public class LoginController : Controller {
        private readonly IHttpContextService httpContextService;
        private readonly ILoginService loginService;

        public LoginController(ILoginService loginService, IHttpContextService httpContextService) {
            this.loginService = loginService;
            this.httpContextService = httpContextService;
        }

        [HttpGet]
        public bool IsUserAuthenticated() => httpContextService.IsUserAuthenticated();

        [HttpGet("refresh"), Authorize]
        public IActionResult RefreshToken() {
            string loginToken = loginService.RegenerateBearerToken(httpContextService.GetUserId());
            return loginToken != null ? (IActionResult) Ok(loginToken) : BadRequest();
        }

        [HttpPost]
        public IActionResult Login([FromBody] JObject body) {
            string email = body.GetValueFromBody("email");
            string password = body.GetValueFromBody("password");

            try {
                GuardUtilites.ValidateString(email, _ => throw new ArgumentException("Email is invalid. Please try again"));
                GuardUtilites.ValidateString(password, _ => throw new ArgumentException("Password is invalid. Please try again"));
            } catch (ArgumentException exception) {
                return BadRequest(new { error = exception.Message });
            }

            try {
                return Ok(loginService.Login(email, password));
            } catch (LoginFailedException e) {
                return BadRequest(new { message = e.Message });
            }
        }
    }
}
