namespace UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps.Mods;

[BuildStep(Name)]
public class BuildStepBuildAir : ModBuildStep
{
    public const string Name = "Build Air";
    private const string ModName = "uksf_air";
    private IBuildProcessTracker _processTracker;

    protected override Task SetupExecute()
    {
        _processTracker = ServiceProvider?.GetService<IBuildProcessTracker>();
        StepLogger.Log("Retrieved services");
        return base.SetupExecute();
    }

    protected override async Task ProcessExecute()
    {
        StepLogger.Log("Running build for Air");

        var toolsPath = Path.Join(GetBuildSourcesPath(), ModName, "tools");
        var releasePath = Path.Join(GetBuildSourcesPath(), ModName, "release", "@uksf_air");
        var buildPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@uksf_air");

        if (IsBuildNeeded(ModName))
        {
            StepLogger.LogSurround("\nRunning make.py...");
            using BuildProcessHelper processHelper = new(StepLogger, Logger, CancellationTokenSource, processTracker: _processTracker, buildId: Build?.Id);
            processHelper.Run(toolsPath, PythonPath, MakeCommand("redirect"), (int)TimeSpan.FromMinutes(1).TotalMilliseconds, true);
            StepLogger.LogSurround("Make.py complete");
        }

        StepLogger.LogSurround("\nMoving Air release to build...");
        await CopyDirectory(releasePath, buildPath);
        StepLogger.LogSurround("Moved Air release to build");
    }
}
