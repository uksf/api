using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps.Mods
{
    [BuildStep(NAME)]
    public class BuildStepBuildAce : ModBuildStep
    {
        public const string NAME = "Build ACE";
        private const string MOD_NAME = "ace";
        private readonly List<string> _allowedOptionals = new() { "ace_compat_rksl_pm_ii", "ace_nouniformrestrictions" };

        protected override async Task ProcessExecute()
        {
            StepLogger.Log("Running build for ACE");

            var toolsPath = Path.Join(GetBuildSourcesPath(), MOD_NAME, "tools");
            var releasePath = Path.Join(GetBuildSourcesPath(), MOD_NAME, "release", "@ace");
            var buildPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@uksf_ace");

            if (IsBuildNeeded(MOD_NAME))
            {
                StepLogger.LogSurround("\nRunning make.py...");
                BuildProcessHelper processHelper = new(StepLogger, CancellationTokenSource, ignoreErrorGateClose: "File written to", ignoreErrorGateOpen: "MakePbo Version");
                processHelper.Run(toolsPath, PythonPath, MakeCommand("redirect"), (int) TimeSpan.FromMinutes(10).TotalMilliseconds);
                StepLogger.LogSurround("Make.py complete");
            }

            StepLogger.LogSurround("\nMoving ACE release to build...");
            await CopyDirectory(releasePath, buildPath);
            StepLogger.LogSurround("Moved ACE release to build");

            StepLogger.LogSurround("\nMoving optionals...");
            await MoveOptionals(buildPath);
            StepLogger.LogSurround("Moved optionals");
        }

        private async Task MoveOptionals(string buildPath)
        {
            var optionalsPath = Path.Join(buildPath, "optionals");
            var addonsPath = Path.Join(buildPath, "addons");
            DirectoryInfo addons = new(addonsPath);
            foreach (var optionalName in _allowedOptionals)
            {
                DirectoryInfo optional = new(Path.Join(optionalsPath, $"@{optionalName}", "addons"));
                var files = GetDirectoryContents(optional);
                await CopyFiles(optional, addons, files);
            }
        }
    }
}
