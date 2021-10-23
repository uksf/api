using System;
using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps.Mods
{
    [BuildStep(NAME)]
    public class BuildStepBuildAir : ModBuildStep
    {
        public const string NAME = "Build Air";
        private const string MOD_NAME = "uksf_air";

        protected override async Task ProcessExecute()
        {
            StepLogger.Log("Running build for Air");

            var toolsPath = Path.Join(GetBuildSourcesPath(), MOD_NAME, "tools");
            var releasePath = Path.Join(GetBuildSourcesPath(), MOD_NAME, "release", "@uksf_air");
            var buildPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@uksf_air");

            if (IsBuildNeeded(MOD_NAME))
            {
                StepLogger.LogSurround("\nRunning make.py...");
                BuildProcessHelper processHelper = new(StepLogger, CancellationTokenSource);
                processHelper.Run(toolsPath, PythonPath, MakeCommand("redirect"), (int) TimeSpan.FromMinutes(1).TotalMilliseconds);
                StepLogger.LogSurround("Make.py complete");
            }

            StepLogger.LogSurround("\nMoving Air release to build...");
            await CopyDirectory(releasePath, buildPath);
            StepLogger.LogSurround("Moved Air release to build");
        }
    }
}
