using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.Common {
    [BuildStep(NAME)]
    public class BuildStepKeys : FileBuildStep {
        public const string NAME = "Keys";

        protected override async Task SetupExecute() {
            await Logger.Log("Wiping server keys folder");
            string keysPath = Path.Join(GetBuildEnvironmentPath(), "Keys");
            await DeleteDirectoryContents(keysPath);
            await Logger.Log("Server keys folder wiped");
        }

        protected override async Task ProcessExecute() {
            await Logger.Log("Updating keys");

            string sourceBasePath = Path.Join(GetBuildEnvironmentPath(), "BaseKeys");
            string sourceRepoPath = Path.Join(GetBuildEnvironmentPath(), "Repo");
            string targetPath = Path.Join(GetBuildEnvironmentPath(), "Keys");

            DirectoryInfo sourceBase = new DirectoryInfo(sourceBasePath);
            DirectoryInfo sourceRepo = new DirectoryInfo(sourceRepoPath);
            DirectoryInfo target = new DirectoryInfo(targetPath);
            List<FileInfo> baseKeys = GetDirectoryContents(sourceRepo, "*.bikey");
            List<FileInfo> repoKeys = GetDirectoryContents(sourceRepo, "*.bikey");
            await Logger.Log($"Found {baseKeys.Count} keys in base keys");
            await Logger.Log($"Found {repoKeys.Count} keys in repo");

            await Logger.LogSurround("\nCopying base keys...");
            await CopyFiles(sourceBase, target, baseKeys, true);
            await Logger.LogSurround("Copied base keys");

            await Logger.LogSurround("\nCopying repo keys...");
            await CopyFiles(sourceRepo, target, repoKeys, true);
            await Logger.LogSurround("Copied repo keys");
        }
    }
}
