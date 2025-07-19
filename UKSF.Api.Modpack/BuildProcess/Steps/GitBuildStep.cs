using UKSF.Api.Core.Services;

namespace UKSF.Api.Modpack.BuildProcess.Steps;

public class GitBuildStep : BuildStep
{
    private IGitService _gitService;

    protected override Task SetupExecute()
    {
        _gitService = ServiceProvider.GetService<IGitService>();

        StepLogger.Log("Retrieved services");
        return Task.CompletedTask;
    }

    internal async Task<string> GitCommand(string workingDirectory, string command)
    {
        return await _gitService.ExecuteCommand(workingDirectory, command, CancellationTokenSource.Token);
    }
}
