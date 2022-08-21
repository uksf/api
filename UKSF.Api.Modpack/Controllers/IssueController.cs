using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Commands;
using UKSF.Api.Shared.Exceptions;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Modpack.Controllers
{
    [Route("issue")]
    [Permissions(Permissions.Member)]
    public class IssueController : ControllerBase
    {
        private readonly IDisplayNameService _displayNameService;
        private readonly string _githubToken;
        private readonly IHttpContextService _httpContextService;
        private readonly ISendBasicEmailCommand _sendBasicEmailCommand;

        public IssueController(
            IDisplayNameService displayNameService,
            ISendBasicEmailCommand sendBasicEmailCommand,
            IConfiguration configuration,
            IHttpContextService httpContextService
        )
        {
            _displayNameService = displayNameService;
            _sendBasicEmailCommand = sendBasicEmailCommand;
            _httpContextService = httpContextService;
            _githubToken = configuration.GetSection("Github")["token"];
        }

        [HttpPost]
        [Authorize]
        public async Task<NewIssueResponse> CreateIssue([FromBody] NewIssueRequest issueRequest)
        {
            var user = _displayNameService.GetDisplayName(_httpContextService.GetUserId());
            issueRequest.Body += $"\n\n---\n_**Submitted by:** {user}_";

            try
            {
                using HttpClient client = new();
                StringContent content = new(
                    JsonConvert.SerializeObject(new { title = issueRequest.Title, body = issueRequest.Body }),
                    Encoding.UTF8,
                    "application/vnd.github.v3.full+json"
                );
                var url = issueRequest.IssueType == NewIssueType.WEBSITE
                    ? "https://api.github.com/repos/uksf/website-issues/issues"
                    : "https://api.github.com/repos/uksf/modpack/issues";
                client.DefaultRequestHeaders.Authorization = new("token", _githubToken);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(user);

                var response = await client.PostAsync(url, content);

                var result = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new(JObject.Parse(result)["message"]?.ToString());
                }

                var issueUrl = JObject.Parse(result)["html_url"]?.ToString();
                await _sendBasicEmailCommand.ExecuteAsync(
                    new(
                        "contact.tim.here@gmail.com",
                        "New Issue Created",
                        $"New {(issueRequest.IssueType == NewIssueType.WEBSITE ? "website" : "modpack")} issue reported by {user}\n\n{issueUrl}"
                    )
                );

                return new() { IssueUrl = issueUrl };
            }
            catch (Exception exception)
            {
                throw new BadRequestException(exception.Message);
            }
        }
    }
}
