using System.Collections.Generic;
using System.Threading.Tasks;
using UKSF.Api.Services.Admin;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.BuildSteps {
    [BuildStep(NAME)]
    public class BuildStepPrep : BuildStep {
        public const string NAME = "Prep";

        protected override async Task ProcessExecute() {
            Logger.Log("Mounting build environment");

            string projectsPath = VariablesWrapper.VariablesDataService().GetSingle("BUILD_PATH_PROJECTS").AsString();
            await BuildProcessHelper.RunPowershell(Logger, CancellationTokenSource.Token, "C:/", new List<string> { $"subst P: \"{projectsPath}\"", "subst" });
        }
    }
}
