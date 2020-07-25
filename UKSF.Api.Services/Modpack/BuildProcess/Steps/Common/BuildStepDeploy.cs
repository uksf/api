using System.IO;
using System.Threading.Tasks;
using UKSF.Api.Models.Game;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.Common {
    [BuildStep(NAME)]
    public class BuildStepDeploy : FileBuildStep {
        public const string NAME = "Deploy";

        protected override async Task ProcessExecute() {
            await Logger.Log("Deploying files from RC to release");

            string sourcePath;
            string targetPath;
            if (Build.environment == GameEnvironment.RELEASE) {
                sourcePath = Path.Join(GetEnvironmentPath(GameEnvironment.RC), "Repo");
                targetPath = Path.Join(GetBuildEnvironmentPath(), "Repo");
            } else {
                sourcePath = Path.Join(GetBuildEnvironmentPath(), "Build");
                targetPath = Path.Join(GetBuildEnvironmentPath(), "Repo");
            }

            await Logger.LogSurround("\nAdding new files...");
            await AddFiles(sourcePath, targetPath);
            await Logger.LogSurround("Added new files");

            await Logger.LogSurround("\nCopying updated files...");
            await UpdateFiles(sourcePath, targetPath);
            await Logger.LogSurround("Copied updated files");

            await Logger.LogSurround("\nDeleting removed files...");
            await DeleteFiles(sourcePath, targetPath);
            await Logger.LogSurround("Deleted removed files");
        }
    }
}
