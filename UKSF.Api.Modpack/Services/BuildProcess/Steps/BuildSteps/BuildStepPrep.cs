using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps;

[BuildStep(Name)]
public class BuildStepPrep : BuildStep
{
    public const string Name = "Prep";

    protected override Task ProcessExecute()
    {
        StepLogger.Log("Mounting build environment");

        var projectsPath = VariablesService.GetVariable("BUILD_PATH_PROJECTS").AsString();
        BuildProcessHelper processHelper = new(StepLogger, Logger, CancellationTokenSource, raiseErrors: false);
        processHelper.Run("C:/", "cmd.exe", $"/c \"subst P: \"{projectsPath}\"\"", (int)TimeSpan.FromSeconds(10).TotalMilliseconds);

        processHelper = new BuildProcessHelper(StepLogger, Logger, CancellationTokenSource, raiseErrors: false);
        processHelper.Run("C:/", "cmd.exe", "/c \"subst\"", (int)TimeSpan.FromSeconds(10).TotalMilliseconds);

        return Task.CompletedTask;
    }
}
