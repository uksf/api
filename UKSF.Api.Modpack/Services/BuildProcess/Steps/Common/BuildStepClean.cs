using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.Common
{
    [BuildStep(NAME)]
    public class BuildStepClean : FileBuildStep
    {
        public const string NAME = "Clean folders";

        protected override async Task ProcessExecute()
        {
            var environmentPath = GetBuildEnvironmentPath();
            if (Build.Environment == GameEnvironment.RELEASE)
            {
                var keysPath = Path.Join(environmentPath, "Backup", "Keys");

                StepLogger.LogSurround("\nCleaning keys backup...");
                await DeleteDirectoryContents(keysPath);
                StepLogger.LogSurround("Cleaned keys backup");
            }
            else
            {
                var path = Path.Join(environmentPath, "Build");
                var repoPath = Path.Join(environmentPath, "Repo");
                DirectoryInfo repo = new(repoPath);

                StepLogger.LogSurround("\nCleaning build folder...");
                await DeleteDirectoryContents(path);
                StepLogger.LogSurround("Cleaned build folder");

                StepLogger.LogSurround("\nCleaning orphaned zsync files...");
                var contentFiles = GetDirectoryContents(repo).Where(x => !x.Name.Contains(".zsync"));
                IEnumerable<FileInfo> zsyncFiles = GetDirectoryContents(repo, "*.zsync");
                var orphanedFiles = zsyncFiles.Where(x => contentFiles.All(y => !x.FullName.Contains(y.FullName))).ToList();
                await DeleteFiles(orphanedFiles);
                StepLogger.LogSurround("Cleaned orphaned zsync files");
            }
        }
    }
}
