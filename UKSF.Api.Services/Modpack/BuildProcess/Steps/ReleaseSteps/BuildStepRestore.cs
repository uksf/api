using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.Release {
    [BuildStep(NAME)]
    public class BuildStepRestore : FileBuildStep {
        public const string NAME = "Restore";

        protected override async Task ProcessExecute() {
            Logger.Log("Restoring previous release");
            string environmentPath = GetBuildEnvironmentPath();
            string repoPath = Path.Join(environmentPath, "Repo");
            string keysPath = Path.Join(environmentPath, "Keys");
            string repoBackupPath = Path.Join(environmentPath, "Backup", "Repo");
            string keysBackupPath = Path.Join(environmentPath, "Backup", "Keys");

            Logger.LogSurround("\nRestoring repo...");
            await DeleteDirectoryContents(repoPath);
            await CopyDirectory(repoBackupPath, repoPath);
            Logger.LogSurround("Restored repo");

            Logger.LogSurround("\nRestoring keys...");
            await DeleteDirectoryContents(keysPath);
            await CopyDirectory(keysBackupPath, keysPath);
            Logger.LogSurround("Restored keys");
        }
    }
}
