namespace UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps.Mods;

[BuildStep(Name)]
public class BuildStepBuildAcre : ModBuildStep
{
    public const string Name = "Build ACRE";
    private const string ModName = "acre";

    protected override async Task ProcessExecute()
    {
        StepLogger.Log("Running build for ACRE");

        var rootPath = Path.Join(GetBuildSourcesPath(), ModName);
        var extensionsPath = Path.Join(rootPath, "extensions");
        var hemttReleasePath = Path.Join(rootPath, ".hemttout", "release");
        var buildPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@uksf_acre2");

        if (IsBuildNeeded(ModName))
        {
            StepLogger.LogSurround("\nBuilding ACRE extensions...");
            foreach (var command in AcreExtensionCommands.For(OperatingSystem.IsWindows()))
            {
                await RunProcess(
                    extensionsPath,
                    command.Executable,
                    command.Arguments,
                    (int)TimeSpan.FromMinutes(10).TotalMilliseconds,
                    log: true,
                    redirectStderrToOutput: true
                );
            }

            StepLogger.LogSurround("Built ACRE extensions");

            StepLogger.LogSurround("\nRunning hemtt release...");
            await RunProcess(rootPath, Hemtt, HemttCommand("release --no-archive"), (int)TimeSpan.FromMinutes(10).TotalMilliseconds, true);
            StepLogger.LogSurround("Hemtt release complete");
        }

        StepLogger.LogSurround("\nMoving ACRE release to build...");
        await CopyDirectory(hemttReleasePath, buildPath);
        StepLogger.LogSurround("Moved ACRE release to build");
    }
}
