﻿using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Services.Personnel;

namespace UKSF.Api.Controllers {
    [Route("[controller]"), Roles(RoleDefinitions.MEMBER)]
    public class IssueController : Controller {
        private readonly IDisplayNameService displayNameService;
        private readonly IEmailService emailService;
        private readonly string githubToken;
        private readonly ISessionService sessionService;

        public IssueController(ISessionService sessionService, IDisplayNameService displayNameService, IEmailService emailService, IConfiguration configuration) {
            this.sessionService = sessionService;
            this.displayNameService = displayNameService;
            this.emailService = emailService;
            githubToken = configuration.GetSection("Secrets")["githubToken"];
        }

        [HttpPut, Authorize]
        public async Task<IActionResult> CreateIssue([FromQuery] int type, [FromBody] JObject data) {
            string title = data["title"].ToString();
            string body = data["body"].ToString();
            string user = displayNameService.GetDisplayName(sessionService.GetContextId());
            body += $"\n\n---\n_**Submitted by:** {user}_";

            string issueUrl;
            try {
                using HttpClient client = new HttpClient();
                StringContent content = new StringContent(JsonConvert.SerializeObject(new {title, body}), Encoding.UTF8, "application/vnd.github.v3.full+json");
                string url = type == 0 ? "https://api.github.com/repos/uksf/website-issues/issues" : "https://api.github.com/repos/uksf/modpack/issues";
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", githubToken);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(user);
                HttpResponseMessage response = await client.PostAsync(url, content);
                string result = await response.Content.ReadAsStringAsync();
                issueUrl = JObject.Parse(result)["html_url"].ToString();
                emailService.SendEmail("contact.tim.here@gmail.com", "New Issue Created", $"New {(type == 0 ? "website" : "modpack")} issue reported by {user}\n\n{issueUrl}");
            } catch (Exception) {
                return BadRequest();
            }

            return Ok(new {issueUrl});
        }
    }
}