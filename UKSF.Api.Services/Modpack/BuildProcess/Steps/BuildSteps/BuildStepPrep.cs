using System.Threading.Tasks;
using UKSF.Api.Services.Admin;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.BuildSteps {
    [BuildStep(NAME)]
    public class BuildStepPrep : BuildStep {
        public const string NAME = "Prep";

        protected override Task ProcessExecute() {
            Logger.Log("Mounting build environment");

            string projectsPath = VariablesWrapper.VariablesDataService().GetSingle("BUILD_PATH_PROJECTS").AsString();
            BuildProcessHelper.RunProcess(Logger, CancellationTokenSource.Token, "C:/", "cmd.exe", $"/c \"subst P: \"{projectsPath}\"");

            return Task.CompletedTask;
        }
    }
}
