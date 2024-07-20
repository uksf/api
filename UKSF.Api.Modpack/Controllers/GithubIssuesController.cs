using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Commands;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Request;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Modpack.Controllers;

[Route("github/issues")]
[Permissions(Permissions.Member)]
public class GithubIssueController(
    IDisplayNameService displayNameService,
    IGithubIssuesService githubIssuesService,
    ISendBasicEmailCommand sendBasicEmailCommand,
    IHttpContextService httpContextService
) : ControllerBase
{
    [HttpGet("templates")]
    [Authorize]
    public Task<List<IssueTemplate>> GetIssueTemplates()
    {
        return githubIssuesService.GetIssueTemplates();
    }

    [HttpPost]
    [Authorize]
    public async Task<NewIssueResponse> CreateIssue([FromBody] NewIssueRequest issueRequest)
    {
        var user = displayNameService.GetDisplayName(httpContextService.GetUserId());
        issueRequest = issueRequest with { Body = issueRequest.Body + $"\n\n---\n_**Submitted by:** {user}_" };

        try
        {
            var issue = await githubIssuesService.CreateIssue(issueRequest);

            return new NewIssueResponse { IssueUrl = issue.Url };
        }
        catch (Exception exception)
        {
            throw new BadRequestException(exception.Message);
        }
    }
}
