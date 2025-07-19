namespace UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps.Mods;

[BuildStep(Name)]
public class BuildStepBuildAce : ModBuildStep
{
    public const string Name = "Build ACE";
    private const string ModName = "ace";
    private readonly List<string> _allowedOptionals = ["ace_nouniformrestrictions"];

    protected override async Task ProcessExecute()
    {
        StepLogger.Log("Running build for ACE");

        var rootPath = Path.Join(GetBuildSourcesPath(), ModName);
        var hemttReleasePath = Path.Join(rootPath, ".hemttout", "release");
        var buildPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@uksf_ace");

        if (IsBuildNeeded(ModName))
        {
            StepLogger.LogSurround("\nRunning hemtt release...");
            await RunProcess(rootPath, "cmd.exe", HemttCommand("release --no-archive"), (int)TimeSpan.FromMinutes(10).TotalMilliseconds, true);
            StepLogger.LogSurround("Hemtt release complete");
        }

        StepLogger.LogSurround("\nMoving ACE release to build...");
        await CopyDirectory(hemttReleasePath, buildPath);
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
