using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using UKSFWebsite.Api.Services;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Controllers {
    [Route("[controller]")]
    public class LoginController : Controller {
        private readonly ILoginService loginService;
        private readonly ISessionService sessionService;

        public LoginController(ILoginService loginService, ISessionService sessionService) {
            this.loginService = loginService;
            this.sessionService = sessionService;
        }

        [HttpGet]
        public IActionResult Get() => Ok(new {isAuthenticated = HttpContext.User.Identity.IsAuthenticated});

        [HttpPost]
        public IActionResult Post([FromBody] JObject loginForm) {
            try {
                string loginToken = loginService.Login(loginForm["email"].ToString(), loginForm["password"].ToString());

                if (loginToken != null) {
                    return Ok(loginToken);
                }

                return BadRequest(new {message = "unsuccessful"});
            } catch (LoginFailedException e) {
                return BadRequest(new {message = e.Message});
            }
        }

        [HttpPost("server")]
        public IActionResult AuthorizeAsServer([FromBody] JObject login) {
            try {
                string loginToken = loginService.Login(login["email"].ToString(), login["password"].ToString());

                if (loginToken != null) {
                    return Ok(loginToken);
                }

                return BadRequest();
            } catch (LoginFailedException) {
                return BadRequest();
            }
        }

        [HttpGet("refresh"), Authorize]
        public IActionResult RefreshToken() {
            string loginToken = loginService.RegenerateToken(sessionService.GetContextId());
            return loginToken != null ? (IActionResult) Ok(loginToken) : BadRequest();
        }
    }
}
