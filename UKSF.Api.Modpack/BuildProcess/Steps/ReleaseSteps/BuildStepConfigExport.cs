using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Modpack.BuildProcess.Steps;

namespace UKSF.Api.Modpack.BuildProcess.Steps.ReleaseSteps;

[BuildStep(Name)]
public class BuildStepConfigExport : BuildStep
{
    public const string Name = "Config Export";

    private IConfigExportService _configExportService;

    protected override Task SetupExecute()
    {
        _configExportService = ServiceProvider.GetService<IConfigExportService>();
        return Task.CompletedTask;
    }

    protected override Task ProcessExecute()
    {
        try
        {
            var triggerResult = _configExportService.Trigger(Build.Version);
            StepLogger.Log($"Config export triggered: outcome={triggerResult.Outcome}, runId={triggerResult.RunId}");
        }
        catch (Exception ex)
        {
            StepLogger.LogErrorContent($"Config export trigger failed (release continues): {ex.Message}");
        }

        return Task.CompletedTask;
    }
}
