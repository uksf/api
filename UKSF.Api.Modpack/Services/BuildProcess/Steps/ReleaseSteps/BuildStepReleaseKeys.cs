using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.ReleaseSteps;

[BuildStep(Name)]
public class BuildStepReleaseKeys : FileBuildStep
{
    public const string Name = "Copy Keys";

    protected override async Task SetupExecute()
    {
        var keysPath = Path.Join(GetBuildEnvironmentPath(), "Keys");

        StepLogger.LogSurround("Wiping release server keys folder");
        await DeleteDirectoryContents(keysPath);
        StepLogger.LogSurround("Release server keys folder wiped");
    }

    protected override async Task ProcessExecute()
    {
        StepLogger.Log("Copy RC keys to release keys folder");

        var keysPath = Path.Join(GetBuildEnvironmentPath(), "Keys");
        var rcKeysPath = Path.Join(GetEnvironmentPath(GameEnvironment.Rc), "Keys");

        StepLogger.LogSurround("\nCopying keys...");
        await CopyDirectory(rcKeysPath, keysPath);
        StepLogger.LogSurround("Copied keys");
    }
}
