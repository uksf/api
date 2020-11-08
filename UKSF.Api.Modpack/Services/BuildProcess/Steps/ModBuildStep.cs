using System.Threading.Tasks;
using UKSF.Api.Admin.Extensions;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps {
    public class ModBuildStep : FileBuildStep {
        protected string PythonPath;

        protected override Task SetupExecute() {
            PythonPath = VariablesService.GetVariable("BUILD_PATH_PYTHON").AsString();
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
