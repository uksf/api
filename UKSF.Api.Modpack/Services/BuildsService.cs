using MongoDB.Driver;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.BuildProcess;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Services;

public interface IBuildsService
{
    IEnumerable<DomainModpackBuild> GetDevBuilds();
    IEnumerable<DomainModpackBuild> GetRcBuilds();
    DomainModpackBuild GetLatestDevBuild();
    DomainModpackBuild GetLatestRcBuild(string version);
    Task UpdateBuild(DomainModpackBuild build, UpdateDefinition<DomainModpackBuild> updateDefinition);
    Task UpdateBuildStep(DomainModpackBuild build, ModpackBuildStep buildStep);
    Task<DomainModpackBuild> CreateDevBuild(string version, GithubCommit commit, NewBuild newBuild = null);
    Task<DomainModpackBuild> CreateRcBuild(string version, GithubCommit commit);
    Task<DomainModpackBuild> CreateReleaseBuild(string version);
    Task SetBuildRunning(DomainModpackBuild build);
    Task SucceedBuild(DomainModpackBuild build);
    Task FailBuild(DomainModpackBuild build);
    Task CancelBuild(DomainModpackBuild build);
    Task<DomainModpackBuild> CreateRebuild(DomainModpackBuild build, string newSha = "");
    Task<int> CancelInterruptedBuilds();
    Task<EmergencyCleanupResult> EmergencyCleanupStuckBuilds();
}

public class BuildsService(
    IBuildsContext buildsContext,
    IBuildStepService buildStepService,
    IAccountContext accountContext,
    IHttpContextService httpContextService,
    IBuildProcessTracker processTracker,
    IUksfLogger logger
) : IBuildsService
{
    public async Task UpdateBuild(DomainModpackBuild build, UpdateDefinition<DomainModpackBuild> updateDefinition)
    {
        await buildsContext.Update(build, updateDefinition);
    }

    public async Task UpdateBuildStep(DomainModpackBuild build, ModpackBuildStep buildStep)
    {
        await buildsContext.Update(build, buildStep);
    }

    public IEnumerable<DomainModpackBuild> GetDevBuilds()
    {
        return buildsContext.Get(x => x.Environment == GameEnvironment.Development);
    }

    public IEnumerable<DomainModpackBuild> GetRcBuilds()
    {
        return buildsContext.Get(x => x.Environment != GameEnvironment.Development);
    }

    public DomainModpackBuild GetLatestDevBuild()
    {
        return GetDevBuilds().FirstOrDefault();
    }

    public DomainModpackBuild GetLatestRcBuild(string version)
    {
        return GetRcBuilds().FirstOrDefault(x => x.Version == version);
    }

    public async Task<DomainModpackBuild> CreateDevBuild(string version, GithubCommit commit, NewBuild newBuild = null)
    {
        var previousBuild = GetLatestDevBuild();
        var builderId = accountContext.GetSingle(x => x.Email == commit.Author)?.Id;
        DomainModpackBuild build = new()
        {
            Version = version,
            BuildNumber = previousBuild?.BuildNumber + 1 ?? 1,
            Environment = GameEnvironment.Development,
            Commit = commit,
            BuilderId = builderId,
            Steps = buildStepService.GetSteps(GameEnvironment.Development),
            EnvironmentVariables = new Dictionary<string, object> { { "configuration", newBuild?.Configuration ?? "development" } }
        };

        if (previousBuild is not null)
        {
            SetEnvironmentVariables(build, previousBuild, newBuild);
        }

        await buildsContext.Add(build);
        return build;
    }

    public async Task<DomainModpackBuild> CreateRcBuild(string version, GithubCommit commit)
    {
        var previousBuild = GetLatestRcBuild(version);
        var builderId = accountContext.GetSingle(x => x.Email == commit.Author)?.Id;
        DomainModpackBuild build = new()
        {
            Version = version,
            BuildNumber = previousBuild?.BuildNumber + 1 ?? 1,
            Environment = GameEnvironment.Rc,
            Commit = commit,
            BuilderId = builderId,
            Steps = buildStepService.GetSteps(GameEnvironment.Rc),
            EnvironmentVariables = new Dictionary<string, object> { { "configuration", "release" } }
        };

        if (previousBuild is not null)
        {
            SetEnvironmentVariables(build, previousBuild);
        }

        await buildsContext.Add(build);
        return build;
    }

    public async Task<DomainModpackBuild> CreateReleaseBuild(string version)
    {
        // There must be at least one RC build to release
        var previousBuild = GetRcBuilds().FirstOrDefault(x => x.Version == version);
        if (previousBuild == null)
        {
            throw new InvalidOperationException("Release build requires at least one RC build");
        }

        DomainModpackBuild build = new()
        {
            Version = version,
            BuildNumber = previousBuild.BuildNumber + 1,
            Environment = GameEnvironment.Release,
            Commit = previousBuild.Commit,
            BuilderId = httpContextService.GetUserId(),
            Steps = buildStepService.GetSteps(GameEnvironment.Release),
            EnvironmentVariables = new Dictionary<string, object> { { "configuration", "release" } }
        };
        build.Commit.Message = "Release deployment (no content changes)";
        await buildsContext.Add(build);
        return build;
    }

    public async Task<DomainModpackBuild> CreateRebuild(DomainModpackBuild build, string newSha = "")
    {
        var latestBuild = build.Environment == GameEnvironment.Development ? GetLatestDevBuild() : GetLatestRcBuild(build.Version);
        DomainModpackBuild rebuild = new()
        {
            Version = latestBuild.Environment == GameEnvironment.Development ? null : latestBuild.Version,
            BuildNumber = latestBuild.BuildNumber + 1,
            IsRebuild = true,
            Environment = latestBuild.Environment,
            Steps = buildStepService.GetSteps(build.Environment),
            Commit = latestBuild.Commit,
            BuilderId = httpContextService.GetUserId(),
            EnvironmentVariables = latestBuild.EnvironmentVariables
        };
        if (!string.IsNullOrEmpty(newSha))
        {
            rebuild.Commit.After = newSha;
        }

        rebuild.Commit.Message = latestBuild.Environment == GameEnvironment.Release
            ? $"Re-deployment of release {rebuild.Version}"
            : $"Rebuild of #{build.BuildNumber}";
        await buildsContext.Add(rebuild);
        return rebuild;
    }

    public async Task SetBuildRunning(DomainModpackBuild build)
    {
        build.Running = true;
        build.StartTime = DateTime.UtcNow;
        await buildsContext.Update(build, Builders<DomainModpackBuild>.Update.Set(x => x.Running, true).Set(x => x.StartTime, DateTime.UtcNow));
    }

    public async Task SucceedBuild(DomainModpackBuild build)
    {
        await FinishBuild(build, build.Steps.Any(x => x.BuildResult == ModpackBuildResult.Warning) ? ModpackBuildResult.Warning : ModpackBuildResult.Success);
    }

    public async Task FailBuild(DomainModpackBuild build)
    {
        await FinishBuild(build, ModpackBuildResult.Failed);
    }

    public async Task CancelBuild(DomainModpackBuild build)
    {
        await FinishBuild(build, build.Steps.Any(x => x.BuildResult == ModpackBuildResult.Warning) ? ModpackBuildResult.Warning : ModpackBuildResult.Cancelled);
    }

    public async Task<int> CancelInterruptedBuilds()
    {
        var builds = buildsContext.Get(x => x.Running || x.Steps.Any(y => y.Running)).ToList();
        if (builds.Count == 0)
        {
            return 0;
        }

        var tasks = builds.Select(async build =>
            {
                var runningStep = build.Steps.FirstOrDefault(x => x.Running);
                if (runningStep is not null)
                {
                    runningStep.Running = false;
                    runningStep.Finished = true;
                    runningStep.EndTime = DateTime.UtcNow;
                    runningStep.BuildResult = ModpackBuildResult.Cancelled;
                    runningStep.Logs.Add(new ModpackBuildStepLogItem { Text = "Build was interrupted", Colour = "goldenrod" });
                    await buildsContext.Update(build, runningStep);
                }

                await FinishBuild(build, ModpackBuildResult.Cancelled);
            }
        );

        await Task.WhenAll(tasks);
        logger.LogAudit($"Marked {builds.Count} interrupted builds as cancelled", "SERVER");
        return builds.Count;
    }

    /// <summary>
    ///     Emergency method to forcibly clean up builds that may be stuck in git operations.
    ///     This method kills only processes created by the build system and cleans up stuck builds.
    /// </summary>
    public async Task<EmergencyCleanupResult> EmergencyCleanupStuckBuilds()
    {
        logger.LogWarning("Emergency cleanup of stuck builds initiated");

        try
        {
            // Get all tracked processes
            var trackedProcesses = processTracker.GetTrackedProcesses().ToList();
            logger.LogInfo($"Found {trackedProcesses.Count} tracked build processes to evaluate for cleanup");

            // Kill tracked processes that have been running too long (>5 minutes)
            var cutoffTime = DateTime.UtcNow.AddMinutes(-5);

            foreach (var trackedProcess in trackedProcesses.Where(p => p.StartTime < cutoffTime))
            {
                logger.LogWarning(
                    $"Killing long-running tracked process {trackedProcess.ProcessId} (running for {(DateTime.UtcNow - trackedProcess.StartTime).TotalMinutes:F1} minutes): {trackedProcess.Description}"
                );
            }

            // Use the process tracker to kill long-running processes
            var killedCount = processTracker.KillTrackedProcesses();

            // Clean up any stuck builds in the database and get the count of cancelled builds
            var cancelledCount = await CancelInterruptedBuilds();

            var result = new EmergencyCleanupResult { ProcessesKilled = killedCount, BuildsCancelled = cancelledCount };

            logger.LogAudit(
                $"Emergency cleanup completed. Killed {killedCount} tracked build processes and cancelled {cancelledCount} stuck builds.",
                "SERVER"
            );
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError("Emergency cleanup failed", ex);
            throw;
        }
    }

    private async Task FinishBuild(DomainModpackBuild build, ModpackBuildResult result)
    {
        build.Running = false;
        build.Finished = true;
        build.BuildResult = result;
        build.EndTime = DateTime.UtcNow;
        await buildsContext.Update(
            build,
            Builders<DomainModpackBuild>.Update.Set(x => x.Running, false)
                                        .Set(x => x.Finished, true)
                                        .Set(x => x.BuildResult, result)
                                        .Set(x => x.EndTime, DateTime.UtcNow)
        );
    }

    private static void SetEnvironmentVariables(DomainModpackBuild build, DomainModpackBuild previousBuild, NewBuild newBuild = null)
    {
        SetEnvironmentVariable(build, previousBuild, "ace_updated", "Build ACE", newBuild?.Ace ?? false);
        SetEnvironmentVariable(build, previousBuild, "acre_updated", "Build ACRE", newBuild?.Acre ?? false);
        SetEnvironmentVariable(build, previousBuild, "uksf_air_updated", "Build Air", newBuild?.Air ?? false);
    }

    private static void SetEnvironmentVariable(DomainModpackBuild build, DomainModpackBuild previousBuild, string key, string stepName, bool force)
    {
        if (force)
        {
            build.EnvironmentVariables[key] = true;
            return;
        }

        // Check if previous build had the env variable set for this step
        if (previousBuild.EnvironmentVariables.TryGetValue(key, out var variable))
        {
            var updated = (bool)variable;
            if (updated)
            {
                // We only want to try it again due to a failed/cancelled or unfinished step
                var step = previousBuild.Steps.FirstOrDefault(x => x.Name == stepName);
                if (step is not null && (!step.Finished || step.BuildResult is ModpackBuildResult.Failed or ModpackBuildResult.Cancelled))
                {
                    build.EnvironmentVariables[key] = true;
                }
            }
        }
    }
}
