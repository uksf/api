using System;
using System.IO;
using System.Threading.Tasks;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.ReleaseSteps {
    [BuildStep(NAME)]
    public class BuildStepMerge : GitBuildStep {
        public const string NAME = "Merge";

        protected override Task ProcessExecute() {
            try {
                // Necessary to get around branch protection rules for master. Runs locally on server using user stored login as credentials
                string modpackPath = Path.Join(GetBuildSourcesPath(), "modpack");
                GitCommand(modpackPath, "git fetch");
                GitCommand(modpackPath, "git checkout -t origin/release");
                GitCommand(modpackPath, "git checkout release");
                GitCommand(modpackPath, "git pull");
                GitCommand(modpackPath, "git checkout -t origin/master");
                GitCommand(modpackPath, "git checkout master");
                GitCommand(modpackPath, "git pull");
                GitCommand(modpackPath, "git merge release");
                GitCommand(modpackPath, "git push -u origin master");
                StepLogger.Log("Release branch merge to master complete");
            } catch (Exception exception) {
                Warning($"Release branch merge to master failed:\n{exception}");
            }

            return Task.CompletedTask;
        }
    }
}
