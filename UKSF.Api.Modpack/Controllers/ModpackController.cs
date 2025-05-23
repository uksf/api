using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.Controllers;

[Route("modpack")]
public class ModpackController(IModpackService modpackService, IGithubService githubService) : ControllerBase
{
    [HttpGet("builds")]
    [Permissions(Permissions.Member)]
    public IEnumerable<DomainModpackBuild> GetBuilds()
    {
        return modpackService.GetDevBuilds();
    }

    [HttpGet("builds/{id}")]
    [Permissions(Permissions.Member)]
    public DomainModpackBuild GetBuild([FromRoute] string id)
    {
        var build = modpackService.GetBuild(id);
        if (build == null)
        {
            throw new NotFoundException("Build does not exist");
        }

        return build;
    }

    [HttpGet("builds/{id}/step/{index:int}")]
    [Permissions(Permissions.Member)]
    public ModpackBuildStep GetBuildStep([FromRoute] string id, [FromRoute] int index)
    {
        var build = modpackService.GetBuild(id);
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

    [HttpGet("builds/{id}/rebuild")]
    [Permissions(Permissions.Admin)]
    public async Task Rebuild([FromRoute] string id)
    {
        var build = modpackService.GetBuild(id);
        if (build == null)
        {
            throw new NotFoundException("Build does not exist");
        }

        await modpackService.Rebuild(build);
    }

    [HttpGet("builds/{id}/cancel")]
    [Permissions(Permissions.Admin)]
    public async Task CancelBuild([FromRoute] string id)
    {
        var build = modpackService.GetBuild(id);
        if (build == null)
        {
            throw new NotFoundException("Build does not exist");
        }

        await modpackService.CancelBuild(build);
    }

    [HttpPost("builds/emergency-cleanup")]
    [Permissions(Permissions.Admin)]
    public async Task<object> EmergencyCleanupStuckBuilds()
    {
        var cleanedProcesses = await modpackService.EmergencyCleanupStuckBuilds();
        return new { message = $"Emergency cleanup completed. Killed {cleanedProcesses} processes", processesKilled = cleanedProcesses };
    }

    [HttpGet("rcs")]
    [Permissions(Permissions.Member)]
    public IEnumerable<DomainModpackBuild> GetReleaseCandidates()
    {
        return modpackService.GetRcBuilds();
    }

    [HttpGet("releases")]
    [Permissions(Permissions.Member)]
    public IEnumerable<DomainModpackRelease> GetReleases()
    {
        return modpackService.GetReleases();
    }

    [HttpPost("releases/{version}")]
    [Permissions(Permissions.Admin)]
    public Task CreateRelease([FromRoute] string version)
    {
        if (!Regex.IsMatch(version, @"^\d+\.\d+\.\d+$"))
        {
            throw new BadRequestException($"Version {version} is not a valid version number");
        }

        return modpackService.CreateReleaseForVersion(version);
    }

    [HttpPatch("releases/{version}")]
    [Permissions(Permissions.Admin)]
    public async Task<DomainModpackRelease> UpdateRelease([FromRoute] string version, [FromBody] DomainModpackRelease release)
    {
        if (!release.IsDraft)
        {
            throw new BadRequestException($"Release {version} is not a draft");
        }

        await modpackService.UpdateReleaseDraft(release);
        return modpackService.GetRelease(version);
    }

    [HttpPut("releases/{version}")]
    [Permissions(Permissions.Admin)]
    public async Task Release([FromRoute] string version)
    {
        await modpackService.Release(version);
    }

    [HttpPut("releases/{version}/changelog")]
    [Permissions(Permissions.Admin)]
    public async Task<DomainModpackRelease> RegenerateChangelog([FromRoute] string version)
    {
        await modpackService.RegenerateReleaseDraftChangelog(version);
        return modpackService.GetRelease(version);
    }

    [HttpPost("newbuild")]
    [Permissions(Permissions.Tester)]
    public async Task NewBuild([FromBody] NewBuild newBuild)
    {
        if (!await githubService.IsReferenceValid(newBuild.Reference))
        {
            throw new BadRequestException($"{newBuild.Reference} cannot be built as its version does not have the required make files");
        }

        await modpackService.NewBuild(newBuild);
    }
}
