using System.IO;
using System.Threading.Tasks;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.ReleaseSteps
{
    [BuildStep(NAME)]
    public class BuildStepReleaseKeys : FileBuildStep
    {
        public const string NAME = "Copy Keys";

        protected override async Task SetupExecute()
        {
            string keysPath = Path.Join(GetBuildEnvironmentPath(), "Keys");

            StepLogger.LogSurround("Wiping release server keys folder");
            await DeleteDirectoryContents(keysPath);
            StepLogger.LogSurround("Release server keys folder wiped");
        }

        protected override async Task ProcessExecute()
        {
            StepLogger.Log("Copy RC keys to release keys folder");

            string keysPath = Path.Join(GetBuildEnvironmentPath(), "Keys");
            string rcKeysPath = Path.Join(GetEnvironmentPath(GameEnvironment.RC), "Keys");

            StepLogger.LogSurround("\nCopying keys...");
            await CopyDirectory(rcKeysPath, keysPath);
            StepLogger.LogSurround("Copied keys");
        }
    }
}
