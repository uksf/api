using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.Controllers;

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

    [HttpGet("releases")]
    [Permissions(Permissions.Member)]
    public IEnumerable<DomainModpackRelease> GetReleases()
    {
        return _modpackService.GetReleases();
    }

    [HttpGet("rcs")]
    [Permissions(Permissions.Member)]
    public IEnumerable<DomainModpackBuild> GetReleaseCandidates()
    {
        return _modpackService.GetRcBuilds();
    }

    [HttpGet("builds")]
    [Permissions(Permissions.Member)]
    public IEnumerable<DomainModpackBuild> GetBuilds()
    {
        return _modpackService.GetDevBuilds();
    }

    [HttpGet("builds/{id}")]
    [Permissions(Permissions.Member)]
    public DomainModpackBuild GetBuild([FromRoute] string id)
    {
        var build = _modpackService.GetBuild(id);
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
        var build = _modpackService.GetBuild(id);
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
        var build = _modpackService.GetBuild(id);
        if (build == null)
        {
            throw new NotFoundException("Build does not exist");
        }

        await _modpackService.Rebuild(build);
    }

    [HttpGet("builds/{id}/cancel")]
    [Permissions(Permissions.Admin)]
    public async Task CancelBuild([FromRoute] string id)
    {
        var build = _modpackService.GetBuild(id);
        if (build == null)
        {
            throw new NotFoundException("Build does not exist");
        }

        await _modpackService.CancelBuild(build);
    }

    [HttpPatch("release/{version}")]
    [Permissions(Permissions.Admin)]
    public async Task UpdateRelease([FromRoute] string version, [FromBody] DomainModpackRelease release)
    {
        if (!release.IsDraft)
        {
            throw new BadRequestException($"Release {version} is not a draft");
        }

        await _modpackService.UpdateReleaseDraft(release);
    }

    [HttpGet("release/{version}")]
    [Permissions(Permissions.Admin)]
    public async Task Release([FromRoute] string version)
    {
        await _modpackService.Release(version);
    }

    [HttpGet("release/{version}/changelog")]
    [Permissions(Permissions.Admin)]
    public async Task<DomainModpackRelease> RegenerateChangelog([FromRoute] string version)
    {
        await _modpackService.RegnerateReleaseDraftChangelog(version);
        return _modpackService.GetRelease(version);
    }

    [HttpPost("newbuild")]
    [Permissions(Permissions.Tester)]
    public async Task NewBuild([FromBody] NewBuild newBuild)
    {
        if (!await _githubService.IsReferenceValid(newBuild.Reference))
        {
            throw new BadRequestException($"{newBuild.Reference} cannot be built as its version does not have the required make files");
        }

        await _modpackService.NewBuild(newBuild);
    }
}
