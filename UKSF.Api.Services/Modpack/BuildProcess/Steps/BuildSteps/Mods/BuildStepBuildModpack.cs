using System;
using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.BuildSteps.Mods {
    [BuildStep(NAME)]
    public class BuildStepBuildModpack : ModBuildStep {
        public const string NAME = "Build UKSF";
        private const string MOD_NAME = "modpack";

        protected override async Task ProcessExecute() {
            UpdateInterval = TimeSpan.FromSeconds(2);

            Logger.Log("Running build for UKSF");

            string toolsPath = Path.Join(GetBuildSourcesPath(), MOD_NAME, "tools");
            string releasePath = Path.Join(GetBuildSourcesPath(), MOD_NAME, "release", "@uksf");
            string buildPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@uksf");

            Logger.LogSurround("\nRunning make.py...");
            await BuildProcessHelper.RunProcess(Logger, CancellationTokenSource, toolsPath, PythonPath, MakeCommand("redirect"), TimeSpan.FromMinutes(5).TotalMilliseconds);
            Logger.LogSurround("Make.py complete");

            Logger.LogSurround("\nMoving UKSF release to build...");
            await CopyDirectory(releasePath, buildPath);
            Logger.LogSurround("Moved UKSF release to build");
        }
    }
}
