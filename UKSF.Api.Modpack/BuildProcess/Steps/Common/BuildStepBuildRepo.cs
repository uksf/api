using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Modpack.BuildProcess.Steps.Common;

[BuildStep(Name)]
public class BuildStepBuildRepo : BuildStep
{
    public const string Name = "Build Repo";

    protected override async Task ProcessExecute()
    {
        var repoName = GetEnvironmentRepoName();
        StepLogger.Log($"Building {repoName} repo");

        var arma3SyncPath = VariablesService.GetVariable("BUILD_PATH_ARMA3SYNC").AsString();
        await RunProcessModern(arma3SyncPath, "Java", $"-jar .\\ArmA3Sync.jar -BUILD {repoName}", (int)TimeSpan.FromMinutes(5).TotalMilliseconds);
    }
}
