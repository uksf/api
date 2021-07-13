using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps.Mods
{
    [BuildStep(NAME)]
    public class BuildStepBuildAcre : ModBuildStep
    {
        public const string NAME = "Build ACRE";
        private const string MOD_NAME = "acre";

        private readonly List<string> _errorExclusions = new() { "Found DirectX", "Linking statically", "Visual Studio 16", "INFO: Building", "Build Type" };

        protected override async Task ProcessExecute()
        {
            StepLogger.Log("Running build for ACRE");

            string toolsPath = Path.Join(GetBuildSourcesPath(), MOD_NAME, "tools");
            string releasePath = Path.Join(GetBuildSourcesPath(), MOD_NAME, "release", "@acre2");
            string buildPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@acre2");

            if (IsBuildNeeded(MOD_NAME))
            {
                StepLogger.LogSurround("\nRunning make.py...");
                BuildProcessHelper processHelper =
                    new(StepLogger, CancellationTokenSource, errorExclusions: _errorExclusions, ignoreErrorGateClose: "File written to", ignoreErrorGateOpen: "MakePbo Version");
                processHelper.Run(toolsPath, PythonPath, MakeCommand("redirect compile"), (int)TimeSpan.FromMinutes(10).TotalMilliseconds);
                StepLogger.LogSurround("Make.py complete");
            }

            StepLogger.LogSurround("\nMoving ACRE release to build...");
            await CopyDirectory(releasePath, buildPath);
            StepLogger.LogSurround("Moved ACRE release to build");
        }
    }
}
