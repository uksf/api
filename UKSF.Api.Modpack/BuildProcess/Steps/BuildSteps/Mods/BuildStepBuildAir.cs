namespace UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps.Mods;

[BuildStep(Name)]
public class BuildStepBuildAir : ModBuildStep
{
    public const string Name = "Build Air";
    private const string ModName = "uksf_air";

    protected override async Task ProcessExecute()
    {
        StepLogger.Log("Running build for Air");

        var rootPath = Path.Join(GetBuildSourcesPath(), ModName);
        var hemttReleasePath = Path.Join(rootPath, ".hemttout", "release");
        var buildPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@uksf_air");

        if (IsBuildNeeded(ModName))
        {
            StepLogger.LogSurround("\nRunning hemtt release...");
            await RunProcess(rootPath, "cmd.exe", HemttCommand("release --no-archive"), (int)TimeSpan.FromMinutes(10).TotalMilliseconds, true);
            StepLogger.LogSurround("Hemtt release complete");
        }

        StepLogger.LogSurround("\nMoving Air release to build...");
        await CopyDirectory(hemttReleasePath, buildPath);
        StepLogger.LogSurround("Moved Air release to build");
    }
}
