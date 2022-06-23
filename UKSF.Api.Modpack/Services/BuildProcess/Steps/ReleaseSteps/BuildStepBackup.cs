using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.ReleaseSteps
{
    [BuildStep(Name)]
    public class BuildStepBackup : FileBuildStep
    {
        public const string Name = "Backup";

        protected override async Task ProcessExecute()
        {
            StepLogger.Log("Backing up current release");

            var environmentPath = GetBuildEnvironmentPath();
            var repoPath = Path.Join(environmentPath, "Repo");
            var keysPath = Path.Join(environmentPath, "Keys");
            var repoBackupPath = Path.Join(environmentPath, "Backup", "Repo");
            var keysBackupPath = Path.Join(environmentPath, "Backup", "Keys");

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
