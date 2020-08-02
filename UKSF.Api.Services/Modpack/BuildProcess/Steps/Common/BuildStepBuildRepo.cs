using System;
using System.Threading.Tasks;
using UKSF.Api.Services.Admin;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.Common {
    [BuildStep(NAME)]
    public class BuildStepBuildRepo : BuildStep {
        public const string NAME = "Build Repo";

        protected override async Task ProcessExecute() {
            string repoName = GetEnvironmentRepoName();
            Logger.Log($"Building {repoName} repo");

            string arma3SyncPath = VariablesWrapper.VariablesDataService().GetSingle("BUILD_PATH_ARMA3SYNC").AsString();
            await BuildProcessHelper.RunProcess(Logger, CancellationTokenSource, arma3SyncPath, "Java", $"-jar .\\ArmA3Sync.jar -BUILD {repoName}", TimeSpan.FromMinutes(5).TotalMilliseconds);
        }
    }
}
