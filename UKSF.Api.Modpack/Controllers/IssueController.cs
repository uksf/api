using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Commands;
using UKSF.Api.Shared.Configuration;
using UKSF.Api.Shared.Exceptions;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Modpack.Controllers;

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
        IHttpContextService httpContextService,
        IOptions<AppSettings> options
    )
    {
        _displayNameService = displayNameService;
        _sendBasicEmailCommand = sendBasicEmailCommand;
        _httpContextService = httpContextService;

        var appSettings = options.Value;
        _githubToken = appSettings.Secrets.Github.Token;
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
                JsonSerializer.Serialize(new { title = issueRequest.Title, body = issueRequest.Body }, DefaultJsonSerializerOptions.Options),
                Encoding.UTF8,
                "application/vnd.github.v3.full+json"
            );
            var url = issueRequest.IssueType == NewIssueType.WEBSITE
                ? "https://api.github.com/repos/uksf/website-issues/issues"
                : "https://api.github.com/repos/uksf/modpack/issues";
            client.DefaultRequestHeaders.Authorization = new("token", _githubToken);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(user);

            var response = await client.PostAsync(url, content);

            var resultString = await response.Content.ReadAsStringAsync();
            var result = JsonNode.Parse(resultString);
            if (!response.IsSuccessStatusCode)
            {
                throw new(result.GetValueFromObject("message"));
            }

            var issueUrl = result.GetValueFromObject("html_url");
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
