using System;
using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps.Mods
{
    [BuildStep(NAME)]
    public class BuildStepBuildModpack : ModBuildStep
    {
        public const string NAME = "Build UKSF";
        private const string MOD_NAME = "modpack";

        protected override async Task ProcessExecute()
        {
            StepLogger.Log("Running build for UKSF");

            var toolsPath = Path.Join(GetBuildSourcesPath(), MOD_NAME, "tools");
            var releasePath = Path.Join(GetBuildSourcesPath(), MOD_NAME, "release", "@uksf");
            var buildPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@uksf");

            StepLogger.LogSurround("\nRunning make.py...");
            BuildProcessHelper processHelper = new(StepLogger, CancellationTokenSource);
            processHelper.Run(toolsPath, PythonPath, MakeCommand("redirect"), (int) TimeSpan.FromMinutes(5).TotalMilliseconds);
            StepLogger.LogSurround("Make.py complete");

            StepLogger.LogSurround("\nMoving UKSF release to build...");
            await CopyDirectory(releasePath, buildPath);
            StepLogger.LogSurround("Moved UKSF release to build");
        }
    }
}
