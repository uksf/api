using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps
{
    [BuildStep(NAME)]
    public class BuildStepIntercept : FileBuildStep
    {
        public const string NAME = "Intercept";

        protected override async Task ProcessExecute()
        {
            var sourcePath = Path.Join(GetBuildSourcesPath(), "modpack", "@intercept");
            var targetPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@intercept");

            StepLogger.LogSurround("\nCleaning intercept directory...");
            await DeleteDirectoryContents(targetPath);
            StepLogger.LogSurround("Cleaned intercept directory");

            StepLogger.LogSurround("\nCopying intercept to build...");
            await CopyDirectory(sourcePath, targetPath);
            StepLogger.LogSurround("Copied intercept to build");
        }
    }
}
