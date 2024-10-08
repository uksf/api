using Microsoft.AspNetCore.SignalR;
using Octokit;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.BuildProcess;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Signalr.Clients;
using UKSF.Api.Modpack.Signalr.Hubs;

namespace UKSF.Api.Modpack.Services;

public interface IModpackService
{
    IEnumerable<DomainModpackRelease> GetReleases();
    IEnumerable<DomainModpackBuild> GetRcBuilds();
    IEnumerable<DomainModpackBuild> GetDevBuilds();
    DomainModpackRelease GetRelease(string version);
    DomainModpackBuild GetBuild(string id);
    Task NewBuild(NewBuild newBuild);
    Task Rebuild(DomainModpackBuild build);
    Task CancelBuild(DomainModpackBuild build);
    Task UpdateReleaseDraft(DomainModpackRelease release);
    Task Release(string version);
    Task RegenerateReleaseDraftChangelog(string version);
    Task CreateDevBuildFromPush(PushWebhookPayload payload);
    Task CreateRcBuildFromPush(PushWebhookPayload payload);
    Task CreateReleaseForVersion(string version);
    void RunQueuedBuilds();
}

public class ModpackService(
    IReleasesContext releasesContext,
    IBuildsContext buildsContext,
    IReleaseService releaseService,
    IBuildsService buildsService,
    IBuildQueueService buildQueueService,
    IGithubService githubService,
    IVersionService versionService,
    IVariablesService variablesService,
    IHubContext<ModpackHub, IModpackClient> modpackHub,
    IHttpContextService httpContextService,
    IUksfLogger logger
) : IModpackService
{
    private const string VersionFile = "addons/main/script_version.hpp";

    public IEnumerable<DomainModpackRelease> GetReleases()
    {
        return releasesContext.Get();
    }

    public IEnumerable<DomainModpackBuild> GetRcBuilds()
    {
        return buildsService.GetRcBuilds();
    }

    public IEnumerable<DomainModpackBuild> GetDevBuilds()
    {
        return buildsService.GetDevBuilds();
    }

    public DomainModpackRelease GetRelease(string version)
    {
        return releaseService.GetRelease(version);
    }

    public DomainModpackBuild GetBuild(string id)
    {
        return buildsContext.GetSingle(x => x.Id == id);
    }

    public async Task NewBuild(NewBuild newBuild)
    {
        var commit = await githubService.GetLatestReferenceCommit(newBuild.Reference);
        if (!string.IsNullOrEmpty(httpContextService.GetUserId()))
        {
            commit.Author = httpContextService.GetUserEmail();
        }

        var version = await githubService.GetReferenceVersion(newBuild.Reference);
        var build = await buildsService.CreateDevBuild(version, commit, newBuild);
        logger.LogAudit($"New build created ({GetBuildName(build)})");
        buildQueueService.QueueBuild(build);
    }

    public async Task Rebuild(DomainModpackBuild build)
    {
        logger.LogAudit($"Rebuild triggered for {GetBuildName(build)}.");
        var rebuild = await buildsService.CreateRebuild(
            build,
            build.Commit.Branch == "None" ? string.Empty : (await githubService.GetLatestReferenceCommit(build.Commit.Branch)).After
        );

        buildQueueService.QueueBuild(rebuild);
    }

    public async Task CancelBuild(DomainModpackBuild build)
    {
        logger.LogAudit($"Build {GetBuildName(build)} cancelled");

        if (buildQueueService.CancelQueued(build.Id))
        {
            await buildsService.CancelBuild(build);
        }
        else
        {
            buildQueueService.Cancel(build.Id);
        }
    }

    public async Task UpdateReleaseDraft(DomainModpackRelease release)
    {
        logger.LogAudit($"Release {release.Version} draft updated");
        await releaseService.UpdateDraft(release);
    }

    public async Task Release(string version)
    {
        var releaseBuild = await buildsService.CreateReleaseBuild(version);
        buildQueueService.QueueBuild(releaseBuild);

        logger.LogAudit($"{version} released");
    }

    public async Task RegenerateReleaseDraftChangelog(string version)
    {
        var release = releaseService.GetRelease(version);
        var newChangelog = await githubService.GenerateChangelog(version);
        release.Changelog = newChangelog;

        logger.LogAudit($"Release {version} draft changelog regenerated from github");
        await releaseService.UpdateDraft(release);
    }

    public async Task CreateDevBuildFromPush(PushWebhookPayload payload)
    {
        var devCommit = await githubService.GetPushEvent(payload);
        var version = await githubService.GetReferenceVersion(payload.Ref.Split('/')[^1]);
        var devBuild = await buildsService.CreateDevBuild(version, devCommit);
        buildQueueService.QueueBuild(devBuild);
    }

    public async Task CreateRcBuildFromPush(PushWebhookPayload payload)
    {
        var rcVersion = await githubService.GetReferenceVersion(payload.Ref.Split('/')[^1]);
        var release = releaseService.GetRelease(rcVersion);
        if (release is { IsDraft: false })
        {
            logger.LogWarning($"An attempt to build a release candidate for version {rcVersion} failed because the version has already been released.");
            return;
        }

        var previousBuild = buildsService.GetLatestRcBuild(rcVersion);
        var rcCommit = await githubService.GetPushEvent(payload, previousBuild is not null ? previousBuild.Commit.After : string.Empty);
        if (previousBuild == null)
        {
            release = await releaseService.MakeDraftRelease(rcVersion, rcCommit);
            await modpackHub.Clients.All.ReceiveRelease(release);
        }

        var rcBuild = await buildsService.CreateRcBuild(rcVersion, rcCommit);
        buildQueueService.QueueBuild(rcBuild);
    }

    public async Task CreateReleaseForVersion(string version)
    {
        var latestVersion = await githubService.GetReferenceVersion("main");
        if (!versionService.IsVersionIncremental(version, latestVersion))
        {
            throw new BadRequestException($"New version is not a valid increment from {latestVersion}");
        }

        var latestRelease = releaseService.GetRelease(latestVersion);
        if (latestRelease is { IsDraft: true })
        {
            throw new BadRequestException($"Cannot create a release for version {version} because the previous version {latestVersion} has not been released.");
        }

        var sourcesPath = variablesService.GetVariable("BUILD_PATH_SOURCES").AsString();
        var modpackSourcePath = Path.Join(sourcesPath, "modpack");
        var gitCommand = new GitCommand(modpackSourcePath, logger);

        gitCommand.ResetAndClean().Fetch();
        gitCommand.Checkout("main").Pull();
        gitCommand.Checkout("release").Pull();

        var versionFileContent = versionService.GetVersionFileContentFromVersion(version);
        await File.WriteAllTextAsync(Path.Join(modpackSourcePath, VersionFile), versionFileContent);

        gitCommand.Commit($"Version {version}").Merge("main").Push("release");

        logger.LogAudit($"New release version {version} created");
    }

    public void RunQueuedBuilds()
    {
        var builds = buildsService.GetDevBuilds().Where(x => !x.Finished && !x.Running).ToList();
        builds = builds.Concat(buildsService.GetRcBuilds().Where(x => !x.Finished && !x.Running)).ToList();
        if (builds.Count == 0)
        {
            return;
        }

        foreach (var build in builds)
        {
            buildQueueService.QueueBuild(build);
        }
    }

    private static string GetBuildName(DomainModpackBuild build)
    {
        return build.Environment switch
        {
            GameEnvironment.Release     => $"release {build.Version}",
            GameEnvironment.Rc          => $"{build.Version} RC# {build.BuildNumber}",
            GameEnvironment.Development => $"#{build.BuildNumber}",
            _                           => throw new ArgumentException("Invalid build environment")
        };
    }
}
