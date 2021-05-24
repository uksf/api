using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.ReleaseSteps
{
    [BuildStep(NAME)]
    public class BuildStepBackup : FileBuildStep
    {
        public const string NAME = "Backup";

        protected override async Task ProcessExecute()
        {
            StepLogger.Log("Backing up current release");

            string environmentPath = GetBuildEnvironmentPath();
            string repoPath = Path.Join(environmentPath, "Repo");
            string keysPath = Path.Join(environmentPath, "Keys");
            string repoBackupPath = Path.Join(environmentPath, "Backup", "Repo");
            string keysBackupPath = Path.Join(environmentPath, "Backup", "Keys");

            StepLogger.LogSurround("\nBacking up repo...");
            await AddFiles(repoPath, repoBackupPath);
            await UpdateFiles(repoPath, repoBackupPath);
            await DeleteFiles(repoPath, repoBackupPath);
            StepLogger.LogSurround("Backed up repo");

            StepLogger.LogSurround("\nBacking up keys...");
            await CopyDirectory(keysPath, keysBackupPath);
            StepLogger.LogSurround("Backed up keys");
        }
    }
}
