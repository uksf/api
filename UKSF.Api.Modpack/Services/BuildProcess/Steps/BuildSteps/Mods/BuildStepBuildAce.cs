namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps.Mods;

[BuildStep(Name)]
public class BuildStepBuildAce : ModBuildStep
{
    public const string Name = "Build ACE";
    private const string ModName = "ace";
    private readonly List<string> _allowedOptionals = new() { "ace_nouniformrestrictions" };

    protected override async Task ProcessExecute()
    {
        StepLogger.Log("Running build for ACE");

        var toolsPath = Path.Join(GetBuildSourcesPath(), ModName, "tools");
        var releasePath = Path.Join(GetBuildSourcesPath(), ModName, "release", "@ace");
        var buildPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@uksf_ace");

        if (IsBuildNeeded(ModName))
        {
            StepLogger.LogSurround("\nRunning make.py...");
            BuildProcessHelper processHelper = new(
                StepLogger,
                CancellationTokenSource,
                ignoreErrorGateClose: "File written to",
                ignoreErrorGateOpen: "MakePbo Version"
            );
            processHelper.Run(toolsPath, PythonPath, MakeCommand("redirect"), (int)TimeSpan.FromMinutes(10).TotalMilliseconds);
            StepLogger.LogSurround("Make.py complete");
        }

        StepLogger.LogSurround("\nMoving ACE release to build...");
        await CopyDirectory(releasePath, buildPath);
        StepLogger.LogSurround("Moved ACE release to build");

        StepLogger.LogSurround("\nMoving optionals...");
        await MoveOptionals(buildPath);
        StepLogger.LogSurround("Moved optionals");
    }

    private async Task MoveOptionals(string buildPath)
    {
        var optionalsPath = Path.Join(buildPath, "optionals");
        var addonsPath = Path.Join(buildPath, "addons");
        DirectoryInfo addons = new(addonsPath);
        foreach (var optionalName in _allowedOptionals)
        {
            DirectoryInfo optional = new(Path.Join(optionalsPath, $"@{optionalName}", "addons"));
            var files = GetDirectoryContents(optional);
            await CopyFiles(optional, addons, files);
        }
    }
}
