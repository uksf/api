using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps.Mods
{
    [BuildStep(Name)]
    public class BuildStepBuildAcre : ModBuildStep
    {
        public const string Name = "Build ACRE";
        private const string ModName = "acre";

        private readonly List<string> _errorExclusions = new() { "Found DirectX", "Linking statically", "Visual Studio 16", "INFO: Building", "Build Type" };

        protected override async Task ProcessExecute()
        {
            StepLogger.Log("Running build for ACRE");

            var toolsPath = Path.Join(GetBuildSourcesPath(), ModName, "tools");
            var releasePath = Path.Join(GetBuildSourcesPath(), ModName, "release", "@acre2");
            var buildPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@acre2");

            if (IsBuildNeeded(ModName))
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
