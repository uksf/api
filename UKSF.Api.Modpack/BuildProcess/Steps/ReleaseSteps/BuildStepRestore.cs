namespace UKSF.Api.Modpack.BuildProcess.Steps.ReleaseSteps;

[BuildStep(Name)]
public class BuildStepRestore : FileBuildStep
{
    public const string Name = "Restore";

    protected override async Task ProcessExecute()
    {
        StepLogger.Log("Restoring previous release");
        var environmentPath = GetBuildEnvironmentPath();
        var repoPath = Path.Join(environmentPath, "Repo");
        var keysPath = Path.Join(environmentPath, "Keys");
        var repoBackupPath = Path.Join(environmentPath, "Backup", "Repo");
        var keysBackupPath = Path.Join(environmentPath, "Backup", "Keys");

        StepLogger.LogSurround("\nRestoring repo...");
        await AddFiles(repoBackupPath, repoPath);
        await UpdateFiles(repoBackupPath, repoPath);
        await DeleteFiles(repoBackupPath, repoPath);
        StepLogger.LogSurround("Restored repo");

        StepLogger.LogSurround("\nRestoring keys...");
        await DeleteDirectoryContents(keysPath);
        await CopyDirectory(keysBackupPath, keysPath);
        StepLogger.LogSurround("Restored keys");
    }
}
