using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.BuildSteps.Mods {
    [BuildStep(NAME)]
    public class BuildStepBuildAcre : ModBuildStep {
        public const string NAME = "Build ACRE";
        private const string MOD_NAME = "acre";
        private readonly List<string> errorExclusions = new List<string> {
            "Found DirectX",
            "Linking statically",
            "Visual Studio 16",
            "INFO: Building",
            "Build Type"
        };

        public override bool CheckGuards() => IsBuildNeeded(MOD_NAME);

        protected override async Task ProcessExecute() {
            Logger.Log("Running build for ACRE");

            string toolsPath = Path.Join(GetBuildSourcesPath(), MOD_NAME, "tools");
            string releasePath = Path.Join(GetBuildSourcesPath(), MOD_NAME, "release", "@acre2");
            string buildPath = Path.Join(GetBuildEnvironmentPath(), "Build", "@acre2");

            Logger.LogSurround("\nRunning make.py...");
            await BuildProcessHelper.RunPowershell(Logger, CancellationTokenSource.Token, toolsPath, new List<string> { MakeCommand("compile") }, true, errorExclusions: errorExclusions);
            Logger.LogSurround("Make.py complete");

            Logger.LogSurround("\nMoving ACRE release to build...");
            await CopyDirectory(releasePath, buildPath);
            Logger.LogSurround("Moved ACRE release to build");
        }
    }
}
