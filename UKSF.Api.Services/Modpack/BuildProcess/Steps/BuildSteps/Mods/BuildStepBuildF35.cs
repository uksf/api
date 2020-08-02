using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.BuildSteps.Mods {
    [BuildStep(NAME)]
    public class BuildStepBuildF35 : ModBuildStep {
        public const string NAME = "Build F-35";
        private const string MOD_NAME = "f35";

        public override bool CheckGuards() => IsBuildNeeded(MOD_NAME);

        protected override async Task ProcessExecute() {
            UpdateInterval = TimeSpan.FromMilliseconds(500);

            Logger.Log("Running build for F-35");

            string toolsPath = Path.Join(GetBuildSourcesPath(), MOD_NAME, "tools");
            string releasePath = Path.Join(GetBuildSourcesPath(), MOD_NAME, "release", "@uksf_f35", "addons");
            string dependenciesPath = Path.Join(GetBuildEnvironmentPath(), "Repo", "@uksf_dependencies", "addons");
            DirectoryInfo release = new DirectoryInfo(releasePath);
            DirectoryInfo dependencies = new DirectoryInfo(dependenciesPath);

            Logger.LogSurround("\nRunning make.py...");
            await BuildProcessHelper.RunProcess(Logger, CancellationTokenSource, toolsPath, PythonPath, MakeCommand("redirect"), TimeSpan.FromMinutes(1).TotalMilliseconds);
            Logger.LogSurround("Make.py complete");

            Logger.LogSurround("\nMoving F-35 pbos to uksf dependencies...");
            List<FileInfo> files = GetDirectoryContents(release, "*.pbo");
            await CopyFiles(release, dependencies, files);
            Logger.LogSurround("Moved F-35 pbos to uksf dependencies");
        }
    }
}
