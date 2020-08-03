using System;
using System.Threading.Tasks;
using UKSF.Api.Services.Admin;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.BuildSteps {
    [BuildStep(NAME)]
    public class BuildStepPrep : BuildStep {
        public const string NAME = "Prep";

        protected override Task ProcessExecute() {
            Logger.Log("Mounting build environment");

            string projectsPath = VariablesWrapper.VariablesDataService().GetSingle("BUILD_PATH_PROJECTS").AsString();
            BuildProcessHelper processHelper = new BuildProcessHelper(Logger, CancellationTokenSource, raiseErrors: false);
            processHelper.Run("C:/", "cmd.exe", $"/c \"subst P: \"{projectsPath}\"\"", (int) TimeSpan.FromSeconds(10).TotalMilliseconds);

            processHelper = new BuildProcessHelper(Logger, CancellationTokenSource, raiseErrors: false);
            processHelper.Run("C:/", "cmd.exe", "/c \"subst\"", (int) TimeSpan.FromSeconds(10).TotalMilliseconds);

            return Task.CompletedTask;
        }
    }
}
