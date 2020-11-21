using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps.Mods {
    [BuildStep(NAME)]
    public class BuildStepBuildF35 : ModBuildStep {
        public const string NAME = "Build F-35";
        private const string MOD_NAME = "f35";

        protected override async Task ProcessExecute() {
            StepLogger.Log("Running build for F-35");

            string toolsPath = Path.Join(GetBuildSourcesPath(), MOD_NAME, "tools");
            string releasePath = Path.Join(GetBuildSourcesPath(), MOD_NAME, "release", "@uksf_f35", "addons");
            string dependenciesPath = Path.Join(GetBuildEnvironmentPath(), "Repo", "@uksf_dependencies", "addons");
            DirectoryInfo release = new(releasePath);
            DirectoryInfo dependencies = new(dependenciesPath);

            if (IsBuildNeeded(MOD_NAME)) {
                StepLogger.LogSurround("\nRunning make.py...");
                BuildProcessHelper processHelper = new(StepLogger, CancellationTokenSource);
                processHelper.Run(toolsPath, PythonPath, MakeCommand("redirect"), (int) TimeSpan.FromMinutes(1).TotalMilliseconds);
                StepLogger.LogSurround("Make.py complete");
            }

            StepLogger.LogSurround("\nMoving F-35 pbos to uksf dependencies...");
            List<FileInfo> files = GetDirectoryContents(release, "*.pbo");
            await CopyFiles(release, dependencies, files);
            StepLogger.LogSurround("Moved F-35 pbos to uksf dependencies");
        }
    }
}
