using System.IO;
using System.Threading.Tasks;
using UKSF.Api.Models.Game;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.Common {
    [BuildStep(NAME)]
    public class BuildStepDeploy : FileBuildStep {
        public const string NAME = "Deploy";

        protected override async Task ProcessExecute() {
            string sourcePath;
            string targetPath;
            if (Build.environment == GameEnvironment.RELEASE) {
                Logger.Log("Deploying files from RC to release");
                sourcePath = Path.Join(GetEnvironmentPath(GameEnvironment.RC), "Repo");
                targetPath = Path.Join(GetBuildEnvironmentPath(), "Repo");
            } else {
                Logger.Log("Deploying files from build to repo");
                sourcePath = Path.Join(GetBuildEnvironmentPath(), "Build");
                targetPath = Path.Join(GetBuildEnvironmentPath(), "Repo");
            }

            Logger.LogSurround("\nAdding new files...");
            await AddFiles(sourcePath, targetPath);
            Logger.LogSurround("Added new files");

            Logger.LogSurround("\nCopying updated files...");
            await UpdateFiles(sourcePath, targetPath);
            Logger.LogSurround("Copied updated files");

            Logger.LogSurround("\nDeleting removed files...");
            await DeleteFiles(sourcePath, targetPath, Build.environment != GameEnvironment.RELEASE);
            Logger.LogSurround("Deleted removed files");
        }
    }
}
