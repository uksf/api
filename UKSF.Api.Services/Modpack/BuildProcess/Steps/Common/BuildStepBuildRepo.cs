using System.IO;
using System.Threading.Tasks;
using UKSF.Api.Services.Admin;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.Common {
    [BuildStep(NAME)]
    public class BuildStepBuildRepo : BuildStep {
        public const string NAME = "Build Repo";

        protected override Task ProcessExecute() {
            string repoName = GetEnvironmentRepoName();
            Logger.Log($"Building {repoName} repo");

            string arma3SyncPath = VariablesWrapper.VariablesDataService().GetSingle("BUILD_PATH_ARMA3SYNC").AsString();
            BuildProcessHelper.RunProcess(Logger, CancellationTokenSource.Token, arma3SyncPath, "Java", $"-jar .\\ArmA3Sync.jar -BUILD {repoName}");

            return Task.CompletedTask;
        }
    }
}
