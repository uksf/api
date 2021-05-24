using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.ReleaseSteps
{
    [BuildStep(NAME)]
    public class BuildStepRestore : FileBuildStep
    {
        public const string NAME = "Restore";

        protected override async Task ProcessExecute()
        {
            StepLogger.Log("Restoring previous release");
            string environmentPath = GetBuildEnvironmentPath();
            string repoPath = Path.Join(environmentPath, "Repo");
            string keysPath = Path.Join(environmentPath, "Keys");
            string repoBackupPath = Path.Join(environmentPath, "Backup", "Repo");
            string keysBackupPath = Path.Join(environmentPath, "Backup", "Keys");

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
}
