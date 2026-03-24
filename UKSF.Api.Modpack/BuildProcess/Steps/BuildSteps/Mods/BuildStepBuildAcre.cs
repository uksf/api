namespace UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps.Mods;

[BuildStep(Name)]
public class BuildStepBuildAcre : ModBuildStep
{
    public const string Name = "Build ACRE";
    private const string ModName = "acre";

    protected override async Task ProcessExecute()
    {
        StepLogger.Log("Running build for ACRE");

        var toolsPath = Path.Join(GetBuildSourcesPath(), ModName, "tools");
        var releasePath = Path.Join(GetBuildSourcesPath(), ModName, "release", "@acre2");
        var buildPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@uksf_acre2");

        if (IsBuildNeeded(ModName))
        {
            StepLogger.LogSurround("\nRunning make.py...");
            await RunProcess(
                toolsPath,
                PythonPath,
                MakeCommand("redirect compile"),
                (int)TimeSpan.FromMinutes(10).TotalMilliseconds,
                log: true,
                redirectStderrToOutput: true
            );
            StepLogger.LogSurround("Make.py complete");
        }

        StepLogger.LogSurround("\nMoving ACRE release to build...");
        await CopyDirectory(releasePath, buildPath);
        StepLogger.LogSurround("Moved ACRE release to build");
    }
}
