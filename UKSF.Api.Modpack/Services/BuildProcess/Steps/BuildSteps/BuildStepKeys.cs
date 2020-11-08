using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps {
    [BuildStep(NAME)]
    public class BuildStepKeys : FileBuildStep {
        public const string NAME = "Keys";

        protected override async Task SetupExecute() {
            Logger.LogSurround("\nWiping server keys folder");
            string keysPath = Path.Join(GetBuildEnvironmentPath(), "Keys");
            await DeleteDirectoryContents(keysPath);
            Logger.LogSurround("Server keys folder wiped");
        }

        protected override async Task ProcessExecute() {
            Logger.Log("Updating keys");

            string sourceBasePath = Path.Join(GetBuildEnvironmentPath(), "BaseKeys");
            string sourceRepoPath = Path.Join(GetBuildEnvironmentPath(), "Repo");
            string targetPath = Path.Join(GetBuildEnvironmentPath(), "Keys");
            DirectoryInfo sourceBase = new DirectoryInfo(sourceBasePath);
            DirectoryInfo sourceRepo = new DirectoryInfo(sourceRepoPath);
            DirectoryInfo target = new DirectoryInfo(targetPath);

            Logger.LogSurround("\nCopying base keys...");
            List<FileInfo> baseKeys = GetDirectoryContents(sourceBase, "*.bikey");
            Logger.Log($"Found {baseKeys.Count} keys in base keys");
            await CopyFiles(sourceBase, target, baseKeys, true);
            Logger.LogSurround("Copied base keys");

            Logger.LogSurround("\nCopying repo keys...");
            List<FileInfo> repoKeys = GetDirectoryContents(sourceRepo, "*.bikey");
            Logger.Log($"Found {repoKeys.Count} keys in repo");
            await CopyFiles(sourceRepo, target, repoKeys, true);
            Logger.LogSurround("Copied repo keys");
        }
    }
}
