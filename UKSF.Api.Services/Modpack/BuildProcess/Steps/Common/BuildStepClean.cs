using System.IO;
using System.Threading.Tasks;
using UKSF.Api.Models.Game;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.Common {
    [BuildStep(NAME)]
    public class BuildStepClean : FileBuildStep {
        public const string NAME = "Clean folders";

        protected override async Task ProcessExecute() {
            await Logger.Log("Cleaning build environment");

            string environmentPath = GetBuildEnvironmentPath();
            if (Build.environment == GameEnvironment.RELEASE) {
                string repoPath = Path.Join(environmentPath, "Backup", "Repo");
                string keysPath = Path.Join(environmentPath, "Backup", "Keys");
                await Logger.LogSurround("\nCleaning backup folder");
                await DeleteDirectoryContents(repoPath);
                await DeleteDirectoryContents(keysPath);
                await Logger.LogSurround("Cleaned backup folder");
            } else {
                string path = Path.Join(environmentPath, "Build");
                await Logger.LogSurround("\nCleaning build folder");
                await DeleteDirectoryContents(path);
                await Logger.LogSurround("Cleaned build folder");
            }
        }
    }
}
