﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Base;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.Controllers {
    [Route("[controller]")]
    public class ModpackController : Controller {
        private readonly IGithubService githubService;
        private readonly IModpackService modpackService;

        public ModpackController(IModpackService modpackService, IGithubService githubService) {
            this.modpackService = modpackService;
            this.githubService = githubService;
        }

        [HttpGet("releases"), Authorize, Permissions(Permissions.MEMBER)]
        public IEnumerable<ModpackRelease> GetReleases() => modpackService.GetReleases();

        [HttpGet("rcs"), Authorize, Permissions(Permissions.MEMBER)]
        public IEnumerable<ModpackBuild> GetReleaseCandidates() => modpackService.GetRcBuilds();

        [HttpGet("builds"), Authorize, Permissions(Permissions.MEMBER)]
        public IEnumerable<ModpackBuild> GetBuilds() => modpackService.GetDevBuilds();

        [HttpGet("builds/{id}"), Authorize, Permissions(Permissions.MEMBER)]
        public IActionResult GetBuild(string id) {
            ModpackBuild build = modpackService.GetBuild(id);
            return build == null ? (IActionResult) BadRequest("Build does not exist") : Ok(build);
        }

        [HttpGet("builds/{id}/step/{index}"), Authorize, Permissions(Permissions.MEMBER)]
        public IActionResult GetBuildStep(string id, int index) {
            ModpackBuild build = modpackService.GetBuild(id);
            if (build == null) {
                return BadRequest("Build does not exist");
            }

            if (build.steps.Count > index) {
                return Ok(build.steps[index]);
            }

            return BadRequest("Build step does not exist");
        }

        [HttpGet("builds/{id}/rebuild"), Authorize, Permissions(Permissions.ADMIN)]
        public async Task<IActionResult> Rebuild(string id) {
            ModpackBuild build = modpackService.GetBuild(id);
            if (build == null) {
                return BadRequest("Build does not exist");
            }

            await modpackService.Rebuild(build);
            return Ok();
        }

        [HttpGet("builds/{id}/cancel"), Authorize, Permissions(Permissions.ADMIN)]
        public async Task<IActionResult> CancelBuild(string id) {
            ModpackBuild build = modpackService.GetBuild(id);
            if (build == null) {
                return BadRequest("Build does not exist");
            }

            await modpackService.CancelBuild(build);
            return Ok();
        }

        [HttpPatch("release/{version}"), Authorize, Permissions(Permissions.ADMIN)]
        public async Task<IActionResult> UpdateRelease(string version, [FromBody] ModpackRelease release) {
            if (!release.isDraft) {
                return BadRequest($"Release {version} is not a draft");
            }

            await modpackService.UpdateReleaseDraft(release);
            return Ok();
        }

        [HttpGet("release/{version}"), Authorize, Permissions(Permissions.ADMIN)]
        public async Task<IActionResult> Release(string version) {
            await modpackService.Release(version);
            return Ok();
        }

        [HttpGet("release/{version}/changelog"), Authorize, Permissions(Permissions.ADMIN)]
        public async Task<IActionResult> RegenerateChangelog(string version) {
            await modpackService.RegnerateReleaseDraftChangelog(version);
            return Ok(modpackService.GetRelease(version));
        }

        [HttpPost("newbuild"), Authorize, Permissions(Permissions.TESTER)]
        public async Task<IActionResult> NewBuild([FromBody] NewBuild newBuild) {
            if (!await githubService.IsReferenceValid(newBuild.reference)) {
                return BadRequest($"{newBuild.reference} cannot be built as its version does not have the required make files");
            }

            await modpackService.NewBuild(newBuild);
            return Ok();
        }
    }
}