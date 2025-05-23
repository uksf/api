using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.Modpack.BuildProcess.Steps.BuildSteps.Mods;

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

        var configuration = GetEnvironmentVariable<string>("configuration");
        if (string.IsNullOrEmpty(configuration))
        {
            throw new Exception("Configuration not set for build");
        }

        StepLogger.Log($"\nConfiguration set to '{configuration}'");

        StepLogger.LogSurround("\nRunning make.py...");
        using var processHelper = new BuildProcessHelper(StepLogger, Logger, CancellationTokenSource);
        processHelper.Run(toolsPath, PythonPath, MakeCommand($"redirect configuration {configuration}"), (int)TimeSpan.FromMinutes(5).TotalMilliseconds, true);
        StepLogger.LogSurround("Make.py complete");

        StepLogger.LogSurround("\nMoving UKSF release to build...");
        await CopyDirectory(releasePath, buildPath);
        StepLogger.LogSurround("Moved UKSF release to build");

        if (Build.Environment == GameEnvironment.Rc)
        {
            StepLogger.LogSurround("\nMoving RC optional...");
            await MoveRcOptional(buildPath);
            StepLogger.LogSurround("Moved RC optionals");
        }
    }

    private Task MoveRcOptional(string buildPath)
    {
        DirectoryInfo addons = new(Path.Join(buildPath, "addons"));
        DirectoryInfo optional = new(Path.Join(buildPath, "optionals", "@uksf_rc", "addons"));

        var files = GetDirectoryContents(optional);
        return CopyFiles(optional, addons, files);
    }
}
