using System.Threading.Tasks;
using UKSF.Api.Services.Admin;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps {
    public class ModBuildStep : FileBuildStep {
        protected string PythonPath;

        protected override Task SetupExecute() {
            PythonPath = VariablesWrapper.VariablesDataService().GetSingle("BUILD_PATH_PYTHON").AsString();
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

        internal static string MakeCommand(string arguments = "") => $"make.py {arguments}";
    }
}
