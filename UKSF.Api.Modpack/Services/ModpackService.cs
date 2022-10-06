using Octokit;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services.BuildProcess;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Modpack.Services;

public interface IModpackService
{
    IEnumerable<ModpackRelease> GetReleases();
    IEnumerable<ModpackBuild> GetRcBuilds();
    IEnumerable<ModpackBuild> GetDevBuilds();
    ModpackRelease GetRelease(string version);
    ModpackBuild GetBuild(string id);
    Task NewBuild(NewBuild newBuild);
    Task Rebuild(ModpackBuild build);
    Task CancelBuild(ModpackBuild build);
    Task UpdateReleaseDraft(ModpackRelease release);
    Task Release(string version);
    Task RegnerateReleaseDraftChangelog(string version);
    Task CreateDevBuildFromPush(PushWebhookPayload payload);
    Task CreateRcBuildFromPush(PushWebhookPayload payload);
    void RunQueuedBuilds();
}

public class ModpackService : IModpackService
{
    private readonly IBuildQueueService _buildQueueService;
    private readonly IBuildsContext _buildsContext;
    private readonly IBuildsService _buildsService;
    private readonly IGithubService _githubService;
    private readonly IHttpContextService _httpContextService;
    private readonly IUksfLogger _logger;
    private readonly IReleasesContext _releasesContext;
    private readonly IReleaseService _releaseService;

    public ModpackService(
        IReleasesContext releasesContext,
        IBuildsContext buildsContext,
        IReleaseService releaseService,
        IBuildsService buildsService,
        IBuildQueueService buildQueueService,
        IGithubService githubService,
        IHttpContextService httpContextService,
        IUksfLogger logger
    )
    {
        _releasesContext = releasesContext;
        _buildsContext = buildsContext;
        _releaseService = releaseService;
        _buildsService = buildsService;
        _buildQueueService = buildQueueService;
        _githubService = githubService;
        _httpContextService = httpContextService;
        _logger = logger;
    }

    public IEnumerable<ModpackRelease> GetReleases()
    {
        return _releasesContext.Get();
    }

    public IEnumerable<ModpackBuild> GetRcBuilds()
    {
        return _buildsService.GetRcBuilds();
    }

    public IEnumerable<ModpackBuild> GetDevBuilds()
    {
        return _buildsService.GetDevBuilds();
    }

    public ModpackRelease GetRelease(string version)
    {
        return _releaseService.GetRelease(version);
    }

    public ModpackBuild GetBuild(string id)
    {
        return _buildsContext.GetSingle(x => x.Id == id);
    }

    public async Task NewBuild(NewBuild newBuild)
    {
        var commit = await _githubService.GetLatestReferenceCommit(newBuild.Reference);
        if (!string.IsNullOrEmpty(_httpContextService.GetUserId()))
        {
            commit.Author = _httpContextService.GetUserEmail();
        }

        var version = await _githubService.GetReferenceVersion(newBuild.Reference);
        var build = await _buildsService.CreateDevBuild(version, commit, newBuild);
        _logger.LogAudit($"New build created ({GetBuildName(build)})");
        _buildQueueService.QueueBuild(build);
    }

    public async Task Rebuild(ModpackBuild build)
    {
        _logger.LogAudit($"Rebuild triggered for {GetBuildName(build)}.");
        var rebuild = await _buildsService.CreateRebuild(
            build,
            build.Commit.Branch == "None" ? string.Empty : (await _githubService.GetLatestReferenceCommit(build.Commit.Branch)).After
        );

        _buildQueueService.QueueBuild(rebuild);
    }

    public async Task CancelBuild(ModpackBuild build)
    {
        _logger.LogAudit($"Build {GetBuildName(build)} cancelled");

        if (_buildQueueService.CancelQueued(build.Id))
        {
            await _buildsService.CancelBuild(build);
        }
        else
        {
            _buildQueueService.Cancel(build.Id);
        }
    }

    public async Task UpdateReleaseDraft(ModpackRelease release)
    {
        _logger.LogAudit($"Release {release.Version} draft updated");
        await _releaseService.UpdateDraft(release);
    }

    public async Task Release(string version)
    {
        var releaseBuild = await _buildsService.CreateReleaseBuild(version);
        _buildQueueService.QueueBuild(releaseBuild);

        _logger.LogAudit($"{version} released");
    }

    public async Task RegnerateReleaseDraftChangelog(string version)
    {
        var release = _releaseService.GetRelease(version);
        var newChangelog = await _githubService.GenerateChangelog(version);
        release.Changelog = newChangelog;

        _logger.LogAudit($"Release {version} draft changelog regenerated from github");
        await _releaseService.UpdateDraft(release);
    }

    public async Task CreateDevBuildFromPush(PushWebhookPayload payload)
    {
        var devCommit = await _githubService.GetPushEvent(payload);
        var version = await _githubService.GetReferenceVersion(payload.Ref);
        var devBuild = await _buildsService.CreateDevBuild(version, devCommit);
        _buildQueueService.QueueBuild(devBuild);
    }

    public async Task CreateRcBuildFromPush(PushWebhookPayload payload)
    {
        var rcVersion = await _githubService.GetReferenceVersion(payload.Ref);
        var release = _releaseService.GetRelease(rcVersion);
        if (release is { IsDraft: false })
        {
            _logger.LogWarning($"An attempt to build a release candidate for version {rcVersion} failed because the version has already been released.");
            return;
        }

        var previousBuild = _buildsService.GetLatestRcBuild(rcVersion);
        var rcCommit = await _githubService.GetPushEvent(payload, previousBuild != null ? previousBuild.Commit.After : string.Empty);
        if (previousBuild == null)
        {
            await _releaseService.MakeDraftRelease(rcVersion, rcCommit);
        }

        var rcBuild = await _buildsService.CreateRcBuild(rcVersion, rcCommit);
        _buildQueueService.QueueBuild(rcBuild);
    }

    public void RunQueuedBuilds()
    {
        var builds = _buildsService.GetDevBuilds().Where(x => !x.Finished && !x.Running).ToList();
        builds = builds.Concat(_buildsService.GetRcBuilds().Where(x => !x.Finished && !x.Running)).ToList();
        if (!builds.Any())
        {
            return;
        }

        foreach (var build in builds)
        {
            _buildQueueService.QueueBuild(build);
        }
    }

    private static string GetBuildName(ModpackBuild build)
    {
        return build.Environment switch
        {
            GameEnvironment.RELEASE     => $"release {build.Version}",
            GameEnvironment.RC          => $"{build.Version} RC# {build.BuildNumber}",
            GameEnvironment.DEVELOPMENT => $"#{build.BuildNumber}",
            _                           => throw new ArgumentException("Invalid build environment")
        };
    }
}
