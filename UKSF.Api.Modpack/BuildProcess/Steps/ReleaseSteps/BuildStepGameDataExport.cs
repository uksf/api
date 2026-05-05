using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Modpack.BuildProcess.Steps;

namespace UKSF.Api.Modpack.BuildProcess.Steps.ReleaseSteps;

[BuildStep(Name)]
public class BuildStepGameDataExport : BuildStep
{
    public const string Name = "Game Data Export";

    private IGameDataExportService _gameDataExportService;

    protected override Task SetupExecute()
    {
        _gameDataExportService = ServiceProvider.GetService<IGameDataExportService>();
        return Task.CompletedTask;
    }

    protected override Task ProcessExecute()
    {
        try
        {
            var triggerResult = _gameDataExportService.Trigger(Build.Version);
            StepLogger.Log($"Game data export triggered: outcome={triggerResult.Outcome}, runId={triggerResult.RunId}");
        }
        catch (Exception ex)
        {
            StepLogger.LogErrorContent($"Game data export trigger failed (release continues): {ex.Message}");
        }

        return Task.CompletedTask;
    }
}
