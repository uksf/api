using System.Threading.Tasks;
using UKSF.Api.Services.Admin;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.Common {
    [BuildStep(NAME)]
    public class BuildStepBuildRepo : BuildStep {
        public const string NAME = "Build Repo";

        protected override async Task ProcessExecute() {
            string repoName = GetEnvironmentRepoName();
            await Logger.Log($"Building {repoName} repo");

            string arma3SyncPath = VariablesWrapper.VariablesDataService().GetSingle("BUILD_ARMA3SYNC_PATH").AsString();
            await BuildProcessHelper.RunPowershell(Logger, true, CancellationTokenSource.Token, $"Java -jar .\\ArmA3Sync.jar -BUILD {repoName}", arma3SyncPath);
        }
    }
}
