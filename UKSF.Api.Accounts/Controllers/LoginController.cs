using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UKSF.Api.Accounts.Services;
using UKSF.Api.Accounts.Services.Auth;
using UKSF.Common;

namespace UKSF.Api.Accounts.Controllers {
    [Route("[controller]")]
    public class LoginController : Controller {
        private readonly ILoginService loginService;
        private readonly ISessionService sessionService;

        public LoginController(ILoginService loginService, ISessionService sessionService) {
            this.loginService = loginService;
            this.sessionService = sessionService;
        }

        [HttpGet]
        public bool IsUserAuthenticated() => HttpContext.User.Identity != null && HttpContext.User.Identity.IsAuthenticated;

        [HttpGet("refresh"), Authorize]
        public IActionResult RefreshToken() {
            string loginToken = loginService.RegenerateBearerToken(sessionService.GetUserId());
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
