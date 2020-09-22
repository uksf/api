using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octokit;
using Octokit.Internal;
using UKSF.Api.Interfaces.Integrations.Github;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Services.Personnel;

namespace UKSF.Api.Controllers.Integrations {
    [Route("[controller]")]
    public class GithubController : Controller {
        private const string PUSH_EVENT = "push";
        private const string REPO_NAME = "modpack";
        private const string MASTER = "refs/heads/master";
        private const string RELEASE = "refs/heads/release";

        private readonly IGithubService githubService;
        private readonly IModpackService modpackService;
        private readonly IReleaseService releaseService;

        public GithubController(IModpackService modpackService, IGithubService githubService, IReleaseService releaseService) {
            this.modpackService = modpackService;
            this.githubService = githubService;
            this.releaseService = releaseService;
        }

        [HttpPost]
        public async Task<IActionResult> GithubWebhook(
            [FromHeader(Name = "x-hub-signature")] string githubSignature,
            [FromHeader(Name = "x-github-event")] string githubEvent,
            [FromBody] JObject body
        ) {
            if (!githubService.VerifySignature(githubSignature, body.ToString(Formatting.None))) {
                return Unauthorized();
            }

            PushWebhookPayload payload = new SimpleJsonSerializer().Deserialize<PushWebhookPayload>(body.ToString());
            if (payload.Repository.Name != REPO_NAME || githubEvent != PUSH_EVENT) {
                return Ok();
            }

            switch (payload.Ref) {
                case MASTER when payload.BaseRef != RELEASE: {
                    await modpackService.CreateDevBuildFromPush(payload);
                    return Ok();
                }
                case RELEASE:
                    await modpackService.CreateRcBuildFromPush(payload);
                    return Ok();
                default: return Ok();
            }
        }

        [HttpGet("branches"), Authorize, Roles(RoleDefinitions.TESTER)]
        public async Task<IActionResult> GetBranches() => Ok(await githubService.GetBranches());

        [HttpGet("populatereleases"), Authorize, Roles(RoleDefinitions.ADMIN)]
        public async Task<IActionResult> Release() {
            List<ModpackRelease> releases = await githubService.GetHistoricReleases();
            await releaseService.AddHistoricReleases(releases);
            return Ok();
        }
    }
}
