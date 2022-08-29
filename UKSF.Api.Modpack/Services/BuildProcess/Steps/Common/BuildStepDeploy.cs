using System.IO;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.Common
{
    [BuildStep(Name)]
    public class BuildStepDeploy : FileBuildStep
    {
        public const string Name = "Deploy";

        protected override async Task ProcessExecute()
        {
            string sourcePath;
            string targetPath;
            if (Build.Environment == GameEnvironment.RELEASE)
            {
                StepLogger.Log("Deploying files from RC to release");
                sourcePath = Path.Join(GetEnvironmentPath(GameEnvironment.RC), "Repo");
                targetPath = Path.Join(GetBuildEnvironmentPath(), "Repo");
            }
            else
            {
                StepLogger.Log("Deploying files from build to repo");
                sourcePath = Path.Join(GetBuildEnvironmentPath(), "Build");
                targetPath = Path.Join(GetBuildEnvironmentPath(), "Repo");
            }

            if (Build.Environment != GameEnvironment.RC)
            {
                StepLogger.LogSurround("\nRemoving RC optional...");
                await RemoveRcOptional();
                StepLogger.LogSurround("Removed RC optional");
            }

            StepLogger.LogSurround("\nRemoving UKSF optionals...");
            RemoveUksfOptionalsFolder();
            StepLogger.LogSurround("Removed UKSF optionals");

            StepLogger.LogSurround("\nAdding new files...");
            await AddFiles(sourcePath, targetPath);
            StepLogger.LogSurround("Added new files");

            StepLogger.LogSurround("\nCopying updated files...");
            await UpdateFiles(sourcePath, targetPath);
            StepLogger.LogSurround("Copied updated files");

            StepLogger.LogSurround("\nDeleting removed files...");
            await DeleteFiles(sourcePath, targetPath, Build.Environment != GameEnvironment.RELEASE);
            StepLogger.LogSurround("Deleted removed files");
        }

        private async Task RemoveRcOptional()
        {
            var buildPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@uksf");
            var addonsPath = Path.Join(buildPath, "addons");
            await DeleteFiles(GetDirectoryContents(new(addonsPath), "uksf_rc.*"));
        }

        private void RemoveUksfOptionalsFolder()
        {
            var buildPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@uksf");
            DeleteDirectories(new() { new(Path.Join(buildPath, "optionals")) });
        }
    }
}
