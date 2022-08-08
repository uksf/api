namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps.Mods;

[BuildStep(Name)]
public class BuildStepBuildModpack : ModBuildStep
{
    public const string Name = "Build UKSF";
    private const string ModName = "modpack";

    protected override async Task ProcessExecute()
    {
        StepLogger.Log("Running build for UKSF");

        var toolsPath = Path.Join(GetBuildSourcesPath(), ModName, "tools");
        var releasePath = Path.Join(GetBuildSourcesPath(), ModName, "release", "@uksf");
        var buildPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@uksf");

        StepLogger.LogSurround("\nRunning make.py...");
        BuildProcessHelper processHelper = new(StepLogger, CancellationTokenSource);
        processHelper.Run(toolsPath, PythonPath, MakeCommand("redirect"), (int)TimeSpan.FromMinutes(5).TotalMilliseconds);
        StepLogger.LogSurround("Make.py complete");

        StepLogger.LogSurround("\nMoving UKSF release to build...");
        await CopyDirectory(releasePath, buildPath);
        StepLogger.LogSurround("Moved UKSF release to build");
    }
}
