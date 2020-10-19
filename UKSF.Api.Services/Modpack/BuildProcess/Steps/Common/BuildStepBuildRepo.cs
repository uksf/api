using System;
using System.Threading.Tasks;
using UKSF.Common;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.Common {
    [BuildStep(NAME)]
    public class BuildStepBuildRepo : BuildStep {
        public const string NAME = "Build Repo";

        protected override Task ProcessExecute() {
            string repoName = GetEnvironmentRepoName();
            Logger.Log($"Building {repoName} repo");

            string arma3SyncPath = VariablesService.GetVariable("BUILD_PATH_ARMA3SYNC").AsString();
            BuildProcessHelper processHelper = new BuildProcessHelper(Logger, CancellationTokenSource);
            processHelper.Run(arma3SyncPath, "Java", $"-jar .\\ArmA3Sync.jar -BUILD {repoName}", (int) TimeSpan.FromMinutes(5).TotalMilliseconds);

            return Task.CompletedTask;
        }
    }
}
