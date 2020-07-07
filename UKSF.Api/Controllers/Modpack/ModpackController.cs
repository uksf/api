using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Octokit;
using UKSF.Api.Interfaces.Integrations.Github;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Models.Integrations.Github;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Services.Personnel;
using UKSF.Common;

namespace UKSF.Api.Controllers.Modpack {
    [Route("[controller]")]
    public class ModpackController : Controller {
        private readonly IBuildsService buildsService;
        private readonly IGithubService githubService;
        private readonly IBuildQueueService buildQueueService;
        private readonly IReleaseService releaseService;

        public ModpackController(IReleaseService releaseService, IBuildsService buildsService, IGithubService githubService, IBuildQueueService buildQueueService) {
            this.releaseService = releaseService;
            this.buildsService = buildsService;
            this.githubService = githubService;
            this.buildQueueService = buildQueueService;
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

            buildQueueService.Cancel(id);
            return Ok();
        }

        [HttpPatch("release/{version}"), Authorize, Roles(RoleDefinitions.ADMIN)]
        public async Task<IActionResult> UpdateRelease(string version, [FromBody] ModpackRelease release) {
            if (!release.isDraft) {
                return BadRequest($"Release {version} is not a draft");
            }

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

            // create message on discord modpack channel
            return Ok();
        }


        // [HttpPost("makerc/{version}"), Authorize, Roles(RoleDefinitions.ADMIN)]
        // public async Task<IActionResult> MakeRcBuild(string version, [FromBody] ModpackBuild build) {
        //     if (releaseService.GetRelease(version) != null) {
        //         return BadRequest($"{version} has already been released");
        //     }
        //
        //     ModpackBuild newBuild = await buildsService.CreateFirstRcBuild(version, build);
        //     Merge mergeResult = await githubService.MergeBranch("release", "dev", $"Release Candidate {version}");
        //     newBuild.commit.after = mergeResult.Sha;
        //     newBuild.commit.message = mergeResult.Commit.Message;
        //     await buildsService.UpdateBuild(buildsService.GetBuildRelease(version).id, newBuild);
        //     await releaseService.MakeDraftRelease(version, build);
        //     buildQueueService.QueueBuild(version, newBuild);
        //
        //     // create message on discord modpack tester channel
        //     return Ok();
        // }



        // [HttpPost("testRelease")]
        // public IActionResult TestRelease() {
        //     buildsService.Data.Add(
        //         new ModpackReleaseCandidate {
        //             version = "5.17.18",
        //             builds = new List<ModpackBuild> {
        //                 new ModpackBuild { buildNumber = 0, isNewVersion = true, commit = new GithubCommit { message = "New version" } }
        //             }
        //         }
        //     );
        //     return Ok();
        // }
        //
        [HttpPost("testBuild")]
        public IActionResult TestBuild([FromBody] ModpackBuild build) {
            buildsService.Data.Add(build);
            return Ok();
        }

        // [HttpGet("testrc")]
        // public async Task<IActionResult> TestRc() {
        //     await buildsService.CreateFirstRcBuild("5.17.17", new ModpackBuild {pushEvent = new GithubPushEvent {after = "202baae0cdceb1211497ca949c50be11ba11e855"}});
        //
        //     return Ok();
        // }

        // [HttpGet("testdraft/{version}")]
        // public async Task<IActionResult> TestDraft(string version) => Ok(await githubService.GenerateChangelog(version));

        // [HttpPost("testBuildLog")]
        // public IActionResult TestBuildLog([FromBody] ModpackBuildStep step) {
        //     ModpackBuildRelease buildRelease = buildsService.Data.GetSingle(x => x.version == "5.17.18");
        //     buildsService.InsertBuild(buildRelease.id, build);
        //     return Ok();
        // }
    }
}
