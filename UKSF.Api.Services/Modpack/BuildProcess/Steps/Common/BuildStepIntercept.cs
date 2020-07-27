using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.Common {
    [BuildStep(NAME)]
    public class BuildStepIntercept : FileBuildStep {
        public const string NAME = "Intercept";

        protected override async Task ProcessExecute() {
            string sourcePath = Path.Join(GetBuildSourcesPath(), "modpack", "@intercept");
            string targetPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@intercept");

            Logger.LogSurround("\nCleaning intercept directory...");
            await DeleteDirectoryContents(targetPath);
            Logger.LogSurround("Cleaned intercept directory");

            Logger.LogSurround("\nCopying intercept to build...");
            await CopyDirectory(sourcePath, targetPath);
            Logger.LogSurround("Copied intercept to build");
        }
    }
}
