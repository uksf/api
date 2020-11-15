using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Modpack.Controllers {
    [Route("[controller]"), Permissions(Permissions.MEMBER)]
    public class IssueController : Controller {
        private readonly IDisplayNameService _displayNameService;
        private readonly IEmailService _emailService;
        private readonly IHttpContextService _httpContextService;
        private readonly string _githubToken;


        public IssueController(IDisplayNameService displayNameService, IEmailService emailService, IConfiguration configuration, IHttpContextService httpContextService) {

            _displayNameService = displayNameService;
            _emailService = emailService;
            _httpContextService = httpContextService;
            _githubToken = configuration.GetSection("Github")["token"];
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> CreateIssue([FromQuery] int type, [FromBody] JObject data) {
            string title = data["title"].ToString();
            string body = data["body"].ToString();
            string user = _displayNameService.GetDisplayName(_httpContextService.GetUserId());
            body += $"\n\n---\n_**Submitted by:** {user}_";

            string issueUrl;
            try {
                using HttpClient client = new HttpClient();
                StringContent content = new StringContent(JsonConvert.SerializeObject(new {title, body}), Encoding.UTF8, "application/vnd.github.v3.full+json");
                string url = type == 0 ? "https://api.github.com/repos/uksf/website-issues/issues" : "https://api.github.com/repos/uksf/modpack/issues";
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", _githubToken);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(user);
                HttpResponseMessage response = await client.PostAsync(url, content);
                string result = await response.Content.ReadAsStringAsync();
                issueUrl = JObject.Parse(result)["html_url"].ToString();
                _emailService.SendEmail("contact.tim.here@gmail.com", "New Issue Created", $"New {(type == 0 ? "website" : "modpack")} issue reported by {user}\n\n{issueUrl}");
            } catch (Exception) {
                return BadRequest();
            }

            return Ok(new {issueUrl});
        }
    }
}
