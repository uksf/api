using UKSF.Api.Core.Extensions;

namespace UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps;

[BuildStep(Name)]
public class BuildStepPrep : BuildStep
{
    public const string Name = "Prep";

    protected override async Task ProcessExecute()
    {
        StepLogger.Log("Mounting build environment");

        var projectsPath = VariablesService.GetVariable("BUILD_PATH_PROJECTS").AsString();
        await RunProcessModern("C:/", "cmd.exe", $"/c \"subst P: \"{projectsPath}\"\"", (int)TimeSpan.FromSeconds(10).TotalMilliseconds, false, false, false);
        await RunProcessModern("C:/", "cmd.exe", "/c \"subst\"", (int)TimeSpan.FromSeconds(10).TotalMilliseconds, false, false, false);
    }
}
