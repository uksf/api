using System.Threading.Tasks;
using UKSF.Api.Services.Admin;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps {
    public class ModBuildStep : FileBuildStep {
        private string pythonPath;

        protected override Task SetupExecute() {
            pythonPath = VariablesWrapper.VariablesDataService().GetSingle("BUILD_PATH_PYTHON").AsString();
            Logger.Log("Retrieved python path");
            return Task.CompletedTask;
        }

        internal bool IsBuildNeeded(string key) {
            if (!GetEnvironmentVariable<bool>($"{key}_updated")) {
                Logger.Log("\nBuild is not needed");
                return false;
            }

            return true;
        }

        internal string MakeCommand(string arguments = "") => $".\"{pythonPath}\" make.py {arguments}";
    }
}
