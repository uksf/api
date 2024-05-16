using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.Common;

[BuildStep(Name)]
public class BuildStepBuildRepo : BuildStep
{
    public const string Name = "Build Repo";

    protected override Task ProcessExecute()
    {
        var repoName = GetEnvironmentRepoName();
        StepLogger.Log($"Building {repoName} repo");

        var arma3SyncPath = VariablesService.GetVariable("BUILD_PATH_ARMA3SYNC").AsString();
        BuildProcessHelper processHelper = new(StepLogger, Logger, CancellationTokenSource);
        processHelper.Run(arma3SyncPath, "Java", $"-jar .\\ArmA3Sync.jar -BUILD {repoName}", (int)TimeSpan.FromMinutes(5).TotalMilliseconds);

        return Task.CompletedTask;
    }
}
