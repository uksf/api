using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.BuildProcess.Steps.ReleaseSteps;

[BuildStep(Name)]
public class BuildStepPublish : BuildStep
{
    public const string Name = "Publish";
    private IReleaseService _releaseService;

    protected override Task SetupExecute()
    {
        _releaseService = ServiceProvider.GetService<IReleaseService>();
        StepLogger.Log("Retrieved services");
        return Task.CompletedTask;
    }

    protected override async Task ProcessExecute()
    {
        await _releaseService.PublishRelease(Build.Version);
        StepLogger.Log("Release published");
    }
}
