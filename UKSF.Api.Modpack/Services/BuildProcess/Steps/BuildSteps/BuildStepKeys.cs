namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps;

[BuildStep(Name)]
public class BuildStepKeys : FileBuildStep
{
    public const string Name = "Keys";

    protected override async Task SetupExecute()
    {
        StepLogger.LogSurround("\nWiping server keys folder");
        var keysPath = Path.Join(GetBuildEnvironmentPath(), "Keys");
        await DeleteDirectoryContents(keysPath);
        StepLogger.LogSurround("Server keys folder wiped");
    }

    protected override async Task ProcessExecute()
    {
        StepLogger.Log("Updating keys");

        var sourceBasePath = Path.Join(GetBuildEnvironmentPath(), "BaseKeys");
        var sourceRepoPath = Path.Join(GetBuildEnvironmentPath(), "Repo");
        var targetPath = Path.Join(GetBuildEnvironmentPath(), "Keys");
        DirectoryInfo sourceBase = new(sourceBasePath);
        DirectoryInfo sourceRepo = new(sourceRepoPath);
        DirectoryInfo target = new(targetPath);

        StepLogger.LogSurround("\nCopying base keys...");
        var baseKeys = GetDirectoryContents(sourceBase, "*.bikey");
        StepLogger.Log($"Found {baseKeys.Count} keys in base keys");
        await CopyFiles(sourceBase, target, baseKeys, true);
        StepLogger.LogSurround("Copied base keys");

        StepLogger.LogSurround("\nCopying repo keys...");
        var repoKeys = GetDirectoryContents(sourceRepo, "*.bikey");
        StepLogger.Log($"Found {repoKeys.Count} keys in repo");
        await CopyFiles(sourceRepo, target, repoKeys, true);
        StepLogger.LogSurround("Copied repo keys");
    }
}
