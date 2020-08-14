using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.Models.Game;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.Common {
    [BuildStep(NAME)]
    public class BuildStepClean : FileBuildStep {
        public const string NAME = "Clean folders";

        protected override async Task ProcessExecute() {
            string environmentPath = GetBuildEnvironmentPath();
            if (Build.environment == GameEnvironment.RELEASE) {
                string keysPath = Path.Join(environmentPath, "Backup", "Keys");

                Logger.LogSurround("\nCleaning keys backup...");
                await DeleteDirectoryContents(keysPath);
                Logger.LogSurround("Cleaned keys backup");
            } else {
                string path = Path.Join(environmentPath, "Build");
                string repoPath = Path.Join(environmentPath, "Repo");
                DirectoryInfo repo = new DirectoryInfo(repoPath);

                Logger.LogSurround("\nCleaning build folder...");
                await DeleteDirectoryContents(path);
                Logger.LogSurround("Cleaned build folder");

                Logger.LogSurround("\nCleaning orphaned zsync files...");
                IEnumerable<FileInfo> contentFiles = GetDirectoryContents(repo).Where(x => !x.Name.Contains(".zsync"));
                IEnumerable<FileInfo> zsyncFiles = GetDirectoryContents(repo, "*.zsync");
                List<FileInfo> orphanedFiles = zsyncFiles.Where(x => contentFiles.All(y => !x.FullName.Contains(y.FullName))).ToList();
                await DeleteFiles(orphanedFiles);
                Logger.LogSurround("Cleaned orphaned zsync files");
            }
        }
    }
}
