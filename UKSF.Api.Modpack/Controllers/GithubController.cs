using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octokit;
using Octokit.Internal;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Exceptions;

namespace UKSF.Api.Modpack.Controllers;

[Route("[controller]")]
public class GithubController : ControllerBase
{
    private const string PushEvent = "push";
    private const string RepoName = "modpack";
    private const string Master = "refs/heads/master";
    private const string Release = "refs/heads/release";

    private readonly IGithubService _githubService;
    private readonly IModpackService _modpackService;
    private readonly IReleaseService _releaseService;

    public GithubController(IModpackService modpackService, IGithubService githubService, IReleaseService releaseService)
    {
        _modpackService = modpackService;
        _githubService = githubService;
        _releaseService = releaseService;
    }

    [HttpPost]
    public async Task GithubWebhook(
        [FromHeader(Name = "x-hub-signature")] string githubSignature,
        [FromHeader(Name = "x-github-event")] string githubEvent,
        [FromBody] JObject body
    )
    {
        if (!_githubService.VerifySignature(githubSignature, body.ToString(Formatting.None)))
        {
            throw new UnauthorizedException();
        }

        var payload = new SimpleJsonSerializer().Deserialize<PushWebhookPayload>(body.ToString());
        if (payload.Repository.Name != RepoName || githubEvent != PushEvent)
        {
            return;
        }

        switch (payload.Ref)
        {
            case Master when payload.BaseRef != Release:
            {
                await _modpackService.CreateDevBuildFromPush(payload);
                return;
            }
            case Release:
                await _modpackService.CreateRcBuildFromPush(payload);
                return;
            default: return;
        }
    }

    [HttpGet("branches")]
    [Permissions(Permissions.Tester)]
    public async Task<List<string>> GetBranches()
    {
        return await _githubService.GetBranches();
    }

    [HttpGet("populatereleases")]
    [Permissions(Permissions.Admin)]
    public async Task PopulateReleases()
    {
        var releases = await _githubService.GetHistoricReleases();
        await _releaseService.AddHistoricReleases(releases);
    }
}
