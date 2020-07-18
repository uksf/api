using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Interfaces.Integrations;
using UKSF.Api.Interfaces.Integrations.Github;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Integrations.Github;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Services.Message;
using UKSF.Api.Services.Personnel;

namespace UKSF.Api.Controllers.Modpack {
    [Route("[controller]")]
    public class ModpackController : Controller {
        private readonly IBuildQueueService buildQueueService;
        private readonly IBuildsService buildsService;
        private readonly IGithubService githubService;
        private readonly IReleaseService releaseService;
        private readonly ISessionService sessionService;

        public ModpackController(IReleaseService releaseService, IBuildsService buildsService, IGithubService githubService, IBuildQueueService buildQueueService, ISessionService sessionService) {
            this.releaseService = releaseService;
            this.buildsService = buildsService;
            this.githubService = githubService;
            this.buildQueueService = buildQueueService;
            this.sessionService = sessionService;
        }

        [HttpGet("releases"), Authorize, Roles(RoleDefinitions.MEMBER)]
        public IActionResult GetReleases() => Ok(releaseService.Data.Get());

        [HttpGet("rcs"), Authorize, Roles(RoleDefinitions.TESTER, RoleDefinitions.SERVERS)]
        public IActionResult GetReleaseCandidates() => Ok(buildsService.GetRcBuilds());

        [HttpGet("builds"), Authorize, Roles(RoleDefinitions.TESTER, RoleDefinitions.SERVERS)]
        public IActionResult GetBuilds() => Ok(buildsService.GetDevBuilds());

        [HttpGet("builds/{id}"), Authorize, Roles(RoleDefinitions.TESTER, RoleDefinitions.SERVERS)]
        public IActionResult GetBuild(string id) {
            ModpackBuild build = buildsService.Data.GetSingle(x => x.id == id);
            if (build == null) {
                return BadRequest("Build does not exist");
            }

            return Ok(build);
        }

        [HttpGet("builds/{id}/rebuild"), Authorize, Roles(RoleDefinitions.ADMIN)]
        public async Task<IActionResult> Rebuild(string id) {
            ModpackBuild build = buildsService.Data.GetSingle(x => x.id == id);
            if (build == null) {
                return BadRequest("Build does not exist");
            }

            LogWrapper.AuditLog($"Rebuild triggered for {build.buildNumber}.");
            ModpackBuild rebuild = await buildsService.CreateRebuild(build);
            buildQueueService.QueueBuild(rebuild);
            return Ok();
        }

        [HttpGet("builds/{id}/cancel"), Authorize, Roles(RoleDefinitions.ADMIN)]
        public IActionResult CancelBuild(string id) {
            ModpackBuild build = buildsService.Data.GetSingle(x => x.id == id);
            if (build == null) {
                return BadRequest("Build does not exist");
            }

            LogWrapper.AuditLog($"Build {build.buildNumber} cancelled");
            buildQueueService.Cancel(id);
            return Ok();
        }

        [HttpPatch("release/{version}"), Authorize, Roles(RoleDefinitions.ADMIN)]
        public async Task<IActionResult> UpdateRelease(string version, [FromBody] ModpackRelease release) {
            if (!release.isDraft) {
                return BadRequest($"Release {version} is not a draft");
            }

            LogWrapper.AuditLog($"Release {version} draft updated");
            await releaseService.UpdateDraft(release);
            return Ok();
        }

        [HttpGet("release/{version}"), Authorize, Roles(RoleDefinitions.ADMIN)]
        public async Task<IActionResult> Release(string version) {
            await releaseService.PublishRelease(version);
            ModpackBuild releaseBuild = await buildsService.CreateReleaseBuild(version);
            buildQueueService.QueueBuild(releaseBuild);
            await githubService.MergeBranch("dev", "release", $"Release {version}");
            await githubService.MergeBranch("master", "dev", $"Release {version}");

            LogWrapper.AuditLog($"{version} released");
            return Ok();
        }

        [HttpGet("newbuild/{reference}"), Authorize, Roles(RoleDefinitions.TESTER)]
        public async Task<IActionResult> NewBuild(string reference) {
            if (!await githubService.IsReferenceValid(reference)) {
                return BadRequest($"{reference} cannot be built as its version does not have the required make files");
            }

            GithubCommit commit = await githubService.GetLatestReferenceCommit(reference);
            if (!string.IsNullOrEmpty(sessionService.GetContextId())) {
                commit.author = sessionService.GetContextEmail();
            }

            ModpackBuild build = await buildsService.CreateDevBuild(commit);
            LogWrapper.AuditLog($"New build created ({build.buildNumber})");
            buildQueueService.QueueBuild(build);
            return Ok();
        }
    }
}
