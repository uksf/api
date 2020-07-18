using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
using UKSF.Api.Services.Message;
using UKSF.Api.Services.Personnel;

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
            if (!githubService.VerifySignature(githubSignature, body.ToString(Formatting.None))) {
                return Unauthorized();
            }

            PushWebhookPayload payload = new SimpleJsonSerializer().Deserialize<PushWebhookPayload>(body.ToString());
            if (payload.Repository.Name != REPO_NAME || githubEvent != PUSH_EVENT) {
                return Ok();
            }

            switch (payload.Ref) {
                case DEV when payload.BaseRef != RELEASE: {
                    await OnPushDev(payload);
                    return Ok();
                }
                case RELEASE:
                    await OnPushRelease(payload);
                    return Ok();
                default: return Ok();
            }
        }

        [HttpGet("branches"), Authorize, Roles(RoleDefinitions.TESTER)]
        public async Task<IActionResult> GetBranches() {
            List<string> branches = await githubService.GetBranches();
            return Ok(branches);
        }

        [HttpGet("populatereleases"), Authorize, Roles(RoleDefinitions.ADMIN)]
        public async Task<IActionResult> Release() {
            List<ModpackRelease> releases = await githubService.GetHistoricReleases();
            await releaseService.AddHistoricReleases(releases);
            return Ok();
        }

        private async Task OnPushDev(PushWebhookPayload payload) {
            GithubCommit devCommit = await githubService.GetPushEvent(payload);
            ModpackBuild devBuild = await buildsService.CreateDevBuild(devCommit);
            buildQueueService.QueueBuild(devBuild);
        }

        private async Task OnPushRelease(PushWebhookPayload payload) {
            string rcVersion = await githubService.GetReferenceVersion(payload.Ref);
            ModpackRelease release = releaseService.GetRelease(rcVersion);
            if (release != null && !release.isDraft) {
                LogWrapper.Log($"An attempt to build a release candidate for version {rcVersion} failed because the version has already been released.");
                return;
            }

            ModpackBuild previousBuild = buildsService.GetLatestRcBuild(rcVersion);
            GithubCommit rcCommit = await githubService.GetPushEvent(payload, previousBuild != null ? previousBuild.commit.after : string.Empty);
            if (previousBuild == null) {
                await releaseService.MakeDraftRelease(rcVersion, rcCommit);
            }

            ModpackBuild rcBuild = await buildsService.CreateRcBuild(rcVersion, rcCommit);
            buildQueueService.QueueBuild(rcBuild);
        }
    }
}
