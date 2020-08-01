using System.IO;
using System.Threading.Tasks;
using UKSF.Api.Models.Game;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.ReleaseSteps {
    [BuildStep(NAME)]
    public class BuildStepReleaseKeys : FileBuildStep {
        public const string NAME = "Copy Keys";

        protected override async Task SetupExecute() {
            string keysPath = Path.Join(GetBuildEnvironmentPath(), "Keys");

            Logger.LogSurround("Wiping release server keys folder");
            await DeleteDirectoryContents(keysPath);
            Logger.LogSurround("Release server keys folder wiped");
        }

        protected override async Task ProcessExecute() {
            Logger.Log("Copy RC keys to release keys folder");

            string keysPath = Path.Join(GetBuildEnvironmentPath(), "Keys");
            string rcKeysPath = Path.Join(GetEnvironmentPath(GameEnvironment.RC), "Keys");

            Logger.LogSurround("\nCopying keys...");
            await CopyDirectory(rcKeysPath, keysPath);
            Logger.LogSurround("Copied keys");
        }
    }
}
