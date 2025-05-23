using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps;

[BuildStep(Name)]
public class BuildStepPrep : BuildStep
{
    public const string Name = "Prep";
    private IBuildProcessTracker _processTracker;

    protected override Task SetupExecute()
    {
        _processTracker = ServiceProvider?.GetService<IBuildProcessTracker>();

        StepLogger.Log("Retrieved services");
        return Task.CompletedTask;
    }

    protected override Task ProcessExecute()
    {
        StepLogger.Log("Mounting build environment");

        var projectsPath = VariablesService.GetVariable("BUILD_PATH_PROJECTS").AsString();
        using BuildProcessHelper processHelper = new(
            StepLogger,
            Logger,
            CancellationTokenSource,
            raiseErrors: false,
            processTracker: _processTracker,
            buildId: Build?.Id
        );
        processHelper.Run("C:/", "cmd.exe", $"/c \"subst P: \"{projectsPath}\"\"", (int)TimeSpan.FromSeconds(10).TotalMilliseconds);

        using BuildProcessHelper delayProcessHelper = new(
            StepLogger,
            Logger,
            CancellationTokenSource,
            raiseErrors: false,
            processTracker: _processTracker,
            buildId: Build?.Id
        );
        delayProcessHelper.Run("C:/", "cmd.exe", "/c \"subst\"", (int)TimeSpan.FromSeconds(10).TotalMilliseconds);

        return Task.CompletedTask;
    }
}
