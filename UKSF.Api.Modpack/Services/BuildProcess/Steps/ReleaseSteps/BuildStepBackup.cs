using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.ReleaseSteps {
    [BuildStep(NAME)]
    public class BuildStepBackup : FileBuildStep {
        public const string NAME = "Backup";

        protected override async Task ProcessExecute() {
            Logger.Log("Backing up current release");

            string environmentPath = GetBuildEnvironmentPath();
            string repoPath = Path.Join(environmentPath, "Repo");
            string keysPath = Path.Join(environmentPath, "Keys");
            string repoBackupPath = Path.Join(environmentPath, "Backup", "Repo");
            string keysBackupPath = Path.Join(environmentPath, "Backup", "Keys");

            Logger.LogSurround("\nBacking up repo...");
            await AddFiles(repoPath, repoBackupPath);
            await UpdateFiles(repoPath, repoBackupPath);
            await DeleteFiles(repoPath, repoBackupPath);
            Logger.LogSurround("Backed up repo");

            Logger.LogSurround("\nBacking up keys...");
            await CopyDirectory(keysPath, keysBackupPath);
            Logger.LogSurround("Backed up keys");
        }
    }
}
