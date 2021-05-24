using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Exceptions;

namespace UKSF.Api.Modpack.Controllers
{
    [Route("[controller]")]
    public class ModpackController : ControllerBase
    {
        private readonly IGithubService _githubService;
        private readonly IModpackService _modpackService;

        public ModpackController(IModpackService modpackService, IGithubService githubService)
        {
            _modpackService = modpackService;
            _githubService = githubService;
        }

        [HttpGet("releases"), Authorize, Permissions(Permissions.MEMBER)]
        public IEnumerable<ModpackRelease> GetReleases()
        {
            return _modpackService.GetReleases();
        }

        [HttpGet("rcs"), Authorize, Permissions(Permissions.MEMBER)]
        public IEnumerable<ModpackBuild> GetReleaseCandidates()
        {
            return _modpackService.GetRcBuilds();
        }

        [HttpGet("builds"), Authorize, Permissions(Permissions.MEMBER)]
        public IEnumerable<ModpackBuild> GetBuilds()
        {
            return _modpackService.GetDevBuilds();
        }

        [HttpGet("builds/{id}"), Authorize, Permissions(Permissions.MEMBER)]
        public ModpackBuild GetBuild(string id)
        {
            ModpackBuild build = _modpackService.GetBuild(id);
            if (build == null)
            {
                throw new NotFoundException("Build does not exist");
            }

            return build;
        }

        [HttpGet("builds/{id}/step/{index}"), Authorize, Permissions(Permissions.MEMBER)]
        public ModpackBuildStep GetBuildStep(string id, int index)
        {
            ModpackBuild build = _modpackService.GetBuild(id);
            if (build == null)
            {
                throw new NotFoundException("Build does not exist");
            }

            if (build.Steps.Count > index)
            {
                return build.Steps[index];
            }

            throw new NotFoundException("Build step does not exist");
        }

        [HttpGet("builds/{id}/rebuild"), Authorize, Permissions(Permissions.ADMIN)]
        public async Task Rebuild(string id)
        {
            ModpackBuild build = _modpackService.GetBuild(id);
            if (build == null)
            {
                throw new NotFoundException("Build does not exist");
            }

            await _modpackService.Rebuild(build);
        }

        [HttpGet("builds/{id}/cancel"), Authorize, Permissions(Permissions.ADMIN)]
        public async Task CancelBuild(string id)
        {
            ModpackBuild build = _modpackService.GetBuild(id);
            if (build == null)
            {
                throw new NotFoundException("Build does not exist");
            }

            await _modpackService.CancelBuild(build);
        }

        [HttpPatch("release/{version}"), Authorize, Permissions(Permissions.ADMIN)]
        public async Task UpdateRelease(string version, [FromBody] ModpackRelease release)
        {
            if (!release.IsDraft)
            {
                throw new BadRequestException($"Release {version} is not a draft");
            }

            await _modpackService.UpdateReleaseDraft(release);
        }

        [HttpGet("release/{version}"), Authorize, Permissions(Permissions.ADMIN)]
        public async Task Release(string version)
        {
            await _modpackService.Release(version);
        }

        [HttpGet("release/{version}/changelog"), Authorize, Permissions(Permissions.ADMIN)]
        public async Task<ModpackRelease> RegenerateChangelog(string version)
        {
            await _modpackService.RegnerateReleaseDraftChangelog(version);
            return _modpackService.GetRelease(version);
        }

        [HttpPost("newbuild"), Authorize, Permissions(Permissions.TESTER)]
        public async Task NewBuild([FromBody] NewBuild newBuild)
        {
            if (!await _githubService.IsReferenceValid(newBuild.Reference))
            {
                throw new BadRequestException($"{newBuild.Reference} cannot be built as its version does not have the required make files");
            }

            await _modpackService.NewBuild(newBuild);
        }
    }
}
