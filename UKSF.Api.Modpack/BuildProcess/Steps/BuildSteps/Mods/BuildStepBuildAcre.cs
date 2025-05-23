namespace UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps.Mods;

[BuildStep(Name)]
public class BuildStepBuildAcre : ModBuildStep
{
    public const string Name = "Build ACRE";
    private const string ModName = "acre";

    private readonly List<string> _errorExclusions = ["Found DirectX", "Linking statically", "Visual Studio 16", "INFO: Building", "Build Type"];
    private IBuildProcessTracker _processTracker;

    protected override Task SetupExecute()
    {
        _processTracker = ServiceProvider?.GetService<IBuildProcessTracker>();
        StepLogger.Log("Retrieved services");
        return base.SetupExecute();
    }

    protected override async Task ProcessExecute()
    {
        StepLogger.Log("Running build for ACRE");

        var toolsPath = Path.Join(GetBuildSourcesPath(), ModName, "tools");
        var releasePath = Path.Join(GetBuildSourcesPath(), ModName, "release", "@acre2");
        var buildPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@uksf_acre2");

        if (IsBuildNeeded(ModName))
        {
            StepLogger.LogSurround("\nRunning make.py...");
            using BuildProcessHelper processHelper = new(
                StepLogger,
                Logger,
                CancellationTokenSource,
                errorExclusions: _errorExclusions,
                ignoreErrorGateClose: "File written to",
                ignoreErrorGateOpen: "MakePbo Version",
                processTracker: _processTracker,
                buildId: Build?.Id
            );
            processHelper.Run(toolsPath, PythonPath, MakeCommand("redirect compile"), (int)TimeSpan.FromMinutes(10).TotalMilliseconds, true);
            StepLogger.LogSurround("Make.py complete");
        }

        StepLogger.LogSurround("\nMoving ACRE release to build...");
        await CopyDirectory(releasePath, buildPath);
        StepLogger.LogSurround("Moved ACRE release to build");
    }
}
