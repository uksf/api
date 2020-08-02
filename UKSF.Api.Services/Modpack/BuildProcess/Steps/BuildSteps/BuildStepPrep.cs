using System;
using System.Threading.Tasks;
using UKSF.Api.Services.Admin;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.BuildSteps {
    [BuildStep(NAME)]
    public class BuildStepPrep : BuildStep {
        public const string NAME = "Prep";

        protected override async Task ProcessExecute() {
            Logger.Log("Mounting build environment");

            string projectsPath = VariablesWrapper.VariablesDataService().GetSingle("BUILD_PATH_PROJECTS").AsString();
            await BuildProcessHelper.RunProcess(Logger, CancellationTokenSource, "C:/", "cmd.exe", $"/c \"subst P: \"{projectsPath}\"", TimeSpan.FromSeconds(10).TotalMilliseconds);
        }
    }
}
