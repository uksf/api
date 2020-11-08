using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.BuildSteps.Mods {
    [BuildStep(NAME)]
    public class BuildStepBuildAce : ModBuildStep {
        public const string NAME = "Build ACE";
        private const string MOD_NAME = "ace";
        private readonly List<string> allowedOptionals = new List<string> { "ace_compat_rksl_pm_ii", "ace_nouniformrestrictions" };

        protected override async Task ProcessExecute() {
            Logger.Log("Running build for ACE");

            string toolsPath = Path.Join(GetBuildSourcesPath(), MOD_NAME, "tools");
            string releasePath = Path.Join(GetBuildSourcesPath(), MOD_NAME, "release", "@ace");
            string buildPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@uksf_ace");

            if (IsBuildNeeded(MOD_NAME)) {
                Logger.LogSurround("\nRunning make.py...");
                BuildProcessHelper processHelper = new BuildProcessHelper(Logger, CancellationTokenSource, ignoreErrorGateClose: "File written to", ignoreErrorGateOpen: "MakePbo Version");
                processHelper.Run(toolsPath, PythonPath, MakeCommand("redirect"), (int) TimeSpan.FromMinutes(10).TotalMilliseconds);
                Logger.LogSurround("Make.py complete");
            }

            Logger.LogSurround("\nMoving ACE release to build...");
            await CopyDirectory(releasePath, buildPath);
            Logger.LogSurround("Moved ACE release to build");

            Logger.LogSurround("\nMoving optionals...");
            await MoveOptionals(buildPath);
            Logger.LogSurround("Moved optionals");
        }

        private async Task MoveOptionals(string buildPath) {
            string optionalsPath = Path.Join(buildPath, "optionals");
            string addonsPath = Path.Join(buildPath, "addons");
            DirectoryInfo addons = new DirectoryInfo(addonsPath);
            foreach (string optionalName in allowedOptionals) {
                DirectoryInfo optional = new DirectoryInfo(Path.Join(optionalsPath, $"@{optionalName}", "addons"));
                List<FileInfo> files = GetDirectoryContents(optional);
                await CopyFiles(optional, addons, files);
            }
        }
    }
}
