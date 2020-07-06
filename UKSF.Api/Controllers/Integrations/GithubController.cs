using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octokit;
using Octokit.Internal;
using UKSF.Api.Interfaces.Integrations.Github;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Models.Integrations.Github;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Controllers.Integrations {
    [Route("[controller]")]
    public class GithubController : Controller {
        private const string PUSH_EVENT = "push";
        private const string REPO_NAME = "BuildTest"; //"modpack";
        private const string MASTER = "refs/heads/master";
        private const string DEV = "refs/heads/dev";
        private const string RELEASE = "refs/heads/release";
        private readonly IBuildsService buildsService;

        private readonly IGithubService githubService;
        private readonly IReleaseService releaseService;
        private readonly IBuildQueueService buildQueueService;

        public GithubController(IGithubService githubService, IBuildsService buildsService, IReleaseService releaseService, IBuildQueueService buildQueueService) {
            this.githubService = githubService;
            this.buildsService = buildsService;
            this.releaseService = releaseService;
            this.buildQueueService = buildQueueService;
        }

        [HttpPost]
        public async Task<IActionResult> GithubWebhook(
            [FromHeader(Name = "x-hub-signature")] string githubSignature,
            [FromHeader(Name = "x-github-event")] string githubEvent,
            [FromBody] JObject body
        ) {
            PushWebhookPayload payload = new SimpleJsonSerializer().Deserialize<PushWebhookPayload>(body.ToString());

            if (payload.Repository.Name != REPO_NAME || githubEvent != PUSH_EVENT) {
                return Ok();
            }

            if (!githubService.VerifySignature(githubSignature, body.ToString(Formatting.None))) {
                return Unauthorized();
            }

            switch (payload.Ref) {
                case DEV when payload.BaseRef == MASTER:
                    string devVersion = await githubService.GetCommitVersion(payload.Ref);
                    ModpackBuild previousDevBuild = buildsService.GetLatestBuild(devVersion);
                    GithubCommit devCommit = await githubService.GetPushEvent(payload, previousDevBuild?.commit.after);
                    ModpackBuild devBuild = await buildsService.CreateDevBuild(devVersion, devCommit);
                    buildQueueService.QueueBuild(devVersion, devBuild);
                    return Ok();
                case RELEASE when payload.BaseRef == null && !payload.HeadCommit.Message.Contains("Release Candidate"):
                    string rcVersion = await githubService.GetCommitVersion(payload.Ref);
                    ModpackBuild previousRcBuild = buildsService.GetLatestBuild(rcVersion);
                    GithubCommit rcCommit = await githubService.GetPushEvent(payload, previousRcBuild?.commit.after);
                    ModpackBuild rcBuild = await buildsService.CreateRcBuild(rcVersion, rcCommit);
                    buildQueueService.QueueBuild(rcVersion, rcBuild);
                    return Ok();
                default: return Ok();
            }
        }

        [HttpGet("populatereleases")]
        public async Task<IActionResult> Release() {
            List<ModpackRelease> releases = await githubService.GetHistoricReleases();
            await releaseService.AddHistoricReleases(releases);
            return Ok();
        }
    }
}
