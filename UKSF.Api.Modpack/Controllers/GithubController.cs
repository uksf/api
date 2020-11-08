using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octokit;
using Octokit.Internal;
using UKSF.Api.Base;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.Controllers {
    [Route("[controller]")]
    public class GithubController : Controller {
        private const string PUSH_EVENT = "push";
        private const string REPO_NAME = "modpack";
        private const string MASTER = "refs/heads/master";
        private const string RELEASE = "refs/heads/release";

        private readonly IGithubService _githubService;
        private readonly IModpackService _modpackService;
        private readonly IReleaseService _releaseService;

        public GithubController(IModpackService modpackService, IGithubService githubService, IReleaseService releaseService) {
            _modpackService = modpackService;
            _githubService = githubService;
            _releaseService = releaseService;
        }

        [HttpPost]
        public async Task<IActionResult> GithubWebhook(
            [FromHeader(Name = "x-hub-signature")] string githubSignature,
            [FromHeader(Name = "x-github-event")] string githubEvent,
            [FromBody] JObject body
        ) {
            if (!_githubService.VerifySignature(githubSignature, body.ToString(Formatting.None))) {
                return Unauthorized();
            }

            PushWebhookPayload payload = new SimpleJsonSerializer().Deserialize<PushWebhookPayload>(body.ToString());
            if (payload.Repository.Name != REPO_NAME || githubEvent != PUSH_EVENT) {
                return Ok();
            }

            switch (payload.Ref) {
                case MASTER when payload.BaseRef != RELEASE: {
                    await _modpackService.CreateDevBuildFromPush(payload);
                    return Ok();
                }
                case RELEASE:
                    await _modpackService.CreateRcBuildFromPush(payload);
                    return Ok();
                default: return Ok();
            }
        }

        [HttpGet("branches"), Authorize, Permissions(Permissions.TESTER)]
        public async Task<IActionResult> GetBranches() => Ok(await _githubService.GetBranches());

        [HttpGet("populatereleases"), Authorize, Permissions(Permissions.ADMIN)]
        public async Task<IActionResult> Release() {
            List<ModpackRelease> releases = await _githubService.GetHistoricReleases();
            await _releaseService.AddHistoricReleases(releases);
            return Ok();
        }
    }
}
