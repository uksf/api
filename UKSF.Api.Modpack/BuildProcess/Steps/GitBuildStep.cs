using UKSF.Api.Core.Services;

namespace UKSF.Api.Modpack.BuildProcess.Steps;

public class GitBuildStep : BuildStep
{
    protected IGitService GitService;

    protected override Task SetupExecute()
    {
        GitService = ServiceProvider.GetService<IGitService>();

        StepLogger.Log("Retrieved services");
        return Task.CompletedTask;
    }
}
