using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UKSF.Api.Models.Game;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.Common {
    [BuildStep(NAME)]
    public class BuildStepClean : FileBuildStep {
        public const string NAME = "Clean folders";

        protected override async Task ProcessExecute() {
            string environmentPath = GetBuildEnvironmentPath();
            if (Build.environment == GameEnvironment.RELEASE) {
                string repoPath = Path.Join(environmentPath, "Backup", "Repo");
                string keysPath = Path.Join(environmentPath, "Backup", "Keys");

                Logger.LogSurround("\nCleaning backup folder");
                Logger.Log("Cleaning repo backup");
                await DeleteDirectoryContents(repoPath);
                Logger.Log("\nCleaning keys backup");
                await DeleteDirectoryContents(keysPath);
                Logger.LogSurround("Cleaned backup folder");
            } else {
                string path = Path.Join(environmentPath, "Build");
                string repoPath = Path.Join(environmentPath, "Repo");
                DirectoryInfo repo = new DirectoryInfo(repoPath);

                Logger.LogSurround("\nCleaning build folder");
                await DeleteDirectoryContents(path);
                Logger.LogSurround("Cleaned build folder");

                Logger.LogSurround("\nCleaning repo zsync files...");
                List<FileInfo> files = GetDirectoryContents(repo, "*.zsync");
                await DeleteFiles(files);
                Logger.LogSurround("Cleaned repo zsync files");
            }
        }
    }
}
