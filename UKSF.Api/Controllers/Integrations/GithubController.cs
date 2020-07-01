using System;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UKSF.Api.Interfaces.Integrations.Github;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Models.Integrations.Github;

namespace UKSF.Api.Controllers.Integrations {
    [Route("[controller]")]
    public class GithubController : Controller {
        private readonly IGithubService githubService;
        private readonly IBuildsService buildsService;

        // move to app setting
        private const string MASTER = "refs/head/master";
        private const string DEV = "refs/head/dev";
        private const string RELEASE = "refs/head/release";
        private const string PUSH_EVENT = "push";
        private const string REPO_NAME = "uksf/modpack";

        public GithubController(IGithubService githubService, IBuildsService buildsService) {
            this.githubService = githubService;
            this.buildsService = buildsService;
        }

        [HttpPost]
        public IActionResult GithubWebhook([FromHeader(Name = "x-hub-signature")] string githubSignature, [FromHeader(Name = "x-github-event")] string githubEvent, [FromBody] JObject body) {
            GithubPushEvent githubPushEvent = JsonConvert.DeserializeObject<GithubPushEvent>(body.ToString());
            if (githubPushEvent.repository.name != REPO_NAME || githubEvent != PUSH_EVENT) {
                return Ok();
            }

            if (!githubService.VerifySignature(githubSignature, body.ToString(Formatting.None))) {
                return Unauthorized();
            }

            switch (githubPushEvent.branch) {
                case DEV when githubPushEvent.baseBranch == MASTER:
                    buildsService.CreateDevBuild(githubPushEvent);
                    return Ok();
                case RELEASE when githubPushEvent.baseBranch == RELEASE:
                    buildsService.CreateRcBuild(githubPushEvent);
                    return Ok();
                default:
                    return Ok();
            }
        }
    }
}
