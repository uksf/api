using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps {
    [BuildStep(NAME)]
    public class BuildStepKeys : FileBuildStep {
        public const string NAME = "Keys";

        protected override async Task SetupExecute() {
            StepLogger.LogSurround("\nWiping server keys folder");
            string keysPath = Path.Join(GetBuildEnvironmentPath(), "Keys");
            await DeleteDirectoryContents(keysPath);
            StepLogger.LogSurround("Server keys folder wiped");
        }

        protected override async Task ProcessExecute() {
            StepLogger.Log("Updating keys");

            string sourceBasePath = Path.Join(GetBuildEnvironmentPath(), "BaseKeys");
            string sourceRepoPath = Path.Join(GetBuildEnvironmentPath(), "Repo");
            string targetPath = Path.Join(GetBuildEnvironmentPath(), "Keys");
            DirectoryInfo sourceBase = new DirectoryInfo(sourceBasePath);
            DirectoryInfo sourceRepo = new DirectoryInfo(sourceRepoPath);
            DirectoryInfo target = new DirectoryInfo(targetPath);

            StepLogger.LogSurround("\nCopying base keys...");
            List<FileInfo> baseKeys = GetDirectoryContents(sourceBase, "*.bikey");
            StepLogger.Log($"Found {baseKeys.Count} keys in base keys");
            await CopyFiles(sourceBase, target, baseKeys, true);
            StepLogger.LogSurround("Copied base keys");

            StepLogger.LogSurround("\nCopying repo keys...");
            List<FileInfo> repoKeys = GetDirectoryContents(sourceRepo, "*.bikey");
            StepLogger.Log($"Found {repoKeys.Count} keys in repo");
            await CopyFiles(sourceRepo, target, repoKeys, true);
            StepLogger.LogSurround("Copied repo keys");
        }
    }
}
