using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Models.Integrations.Github;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Services.Personnel;

namespace UKSF.Api.Controllers.Modpack {
    [Route("[controller]")]
    public class ModpackController : Controller {
        private readonly IBuildsService buildsService;
        private readonly IModpackService modpackService;
        private readonly IReleaseService releaseService;

        public ModpackController(IReleaseService releaseService, IBuildsService buildsService, IModpackService modpackService) {
            this.releaseService = releaseService;
            this.buildsService = buildsService;
            this.modpackService = modpackService;
        }

        [HttpGet("releases"), Authorize, Roles(RoleDefinitions.MEMBER)]
        public IActionResult GetReleases() => Ok(releaseService.Data.Get());

        [HttpGet("builds"), Authorize, Roles(RoleDefinitions.TESTER, RoleDefinitions.SERVERS)]
        public IActionResult GetBuilds() => Ok(buildsService.Data.Get());

        [HttpGet("builds/{version}/{buildNumber}")]
        public IActionResult GetBuild(string version, int buildNumber) {
            ModpackBuild build = buildsService.Data.GetSingle(x => x.version == version).builds.FirstOrDefault(x => x.buildNumber == buildNumber);
            if (build == null) {
                return BadRequest("Build does not exist");
            }

            return Ok(build);
        }

        [HttpPost("makerc/{version}")]
        public IActionResult MakeRcBuild(string version) {
            ModpackBuildRelease buildRelease = buildsService.Data.GetSingle(x => x.version == version);

            return Ok();
        }

        [HttpPost("testRelease")]
        public IActionResult TestRelease() {
            buildsService.Data.Add(
                new ModpackBuildRelease {
                    version = "5.17.18",
                    builds = new List<ModpackBuild> {
                        new ModpackBuild { buildNumber = 0, isNewVersion = true, pushEvent = new GithubPushEvent { commit = new GithubCommit { message = "New version" } } }
                    }
                }
            );
            return Ok();
        }

        [HttpPost("testBuild")]
        public IActionResult TestBuild([FromBody] ModpackBuild build) {
            ModpackBuildRelease buildRelease = buildsService.Data.GetSingle(x => x.version == "5.17.18");
            build.buildNumber = buildRelease.builds.First().buildNumber + 1;
            buildsService.InsertBuild(buildRelease.id, build);
            return Ok();
        }

        [HttpGet("test")]
        public async Task<IActionResult> Test() {
            string version = await buildsService.GetBranchVersion("refs/head/dev");

            return Ok();
        }

        // [HttpPost("testBuildLog")]
        // public IActionResult TestBuildLog([FromBody] ModpackBuildStep step) {
        //     ModpackBuildRelease buildRelease = buildsService.Data.GetSingle(x => x.version == "5.17.18");
        //     buildsService.InsertBuild(buildRelease.id, build);
        //     return Ok();
        // }
    }
}
