using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Modpack.BuildProcess.Steps.Common;

[BuildStep(Name)]
public class BuildStepBuildRepo : BuildStep
{
    public const string Name = "Build Repo";
    private IBuildProcessTracker _processTracker;

    protected override Task SetupExecute()
    {
        _processTracker = ServiceProvider?.GetService<IBuildProcessTracker>();

        StepLogger.Log("Retrieved services");
        return Task.CompletedTask;
    }

    protected override Task ProcessExecute()
    {
        var repoName = GetEnvironmentRepoName();
        StepLogger.Log($"Building {repoName} repo");

        var arma3SyncPath = VariablesService.GetVariable("BUILD_PATH_ARMA3SYNC").AsString();
        using BuildProcessHelper processHelper = new(StepLogger, Logger, CancellationTokenSource, processTracker: _processTracker, buildId: Build?.Id);
        processHelper.Run(arma3SyncPath, "Java", $"-jar .\\ArmA3Sync.jar -BUILD {repoName}", (int)TimeSpan.FromMinutes(5).TotalMilliseconds);

        return Task.CompletedTask;
    }
}
