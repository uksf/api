using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octokit;
using Octokit.Internal;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Exceptions;

namespace UKSF.Api.Modpack.Controllers
{
    [Route("[controller]")]
    public class GithubController : Controller
    {
        private const string PUSH_EVENT = "push";
        private const string REPO_NAME = "modpack";
        private const string MASTER = "refs/heads/master";
        private const string RELEASE = "refs/heads/release";

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
        public async Task GithubWebhook([FromHeader(Name = "x-hub-signature")] string githubSignature, [FromHeader(Name = "x-github-event")] string githubEvent, [FromBody] JObject body)
        {
            if (!_githubService.VerifySignature(githubSignature, body.ToString(Formatting.None)))
            {
                throw new UnauthorizedException();
            }

            PushWebhookPayload payload = new SimpleJsonSerializer().Deserialize<PushWebhookPayload>(body.ToString());
            if (payload.Repository.Name != REPO_NAME || githubEvent != PUSH_EVENT)
            {
                return;
            }

            switch (payload.Ref)
            {
                case MASTER when payload.BaseRef != RELEASE:
                {
                    await _modpackService.CreateDevBuildFromPush(payload);
                    return;
                }
                case RELEASE:
                    await _modpackService.CreateRcBuildFromPush(payload);
                    return;
                default: return;
            }
        }

        [HttpGet("branches"), Authorize, Permissions(Permissions.TESTER)]
        public async Task<List<string>> GetBranches()
        {
            return await _githubService.GetBranches();
        }

        [HttpGet("populatereleases"), Authorize, Permissions(Permissions.ADMIN)]
        public async Task Release()
        {
            List<ModpackRelease> releases = await _githubService.GetHistoricReleases();
            await _releaseService.AddHistoricReleases(releases);
        }
    }
}
