using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Interfaces.Integrations.Github;
using UKSF.Api.Services.Common;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.ReleaseSteps {
    [BuildStep(NAME)]
    public class BuildStepMerge : GitBuildStep {
        public const string NAME = "Merge";
        private IGithubService githubService;

        protected override Task SetupExecute() {
            githubService = ServiceWrapper.Provider.GetService<IGithubService>();
            Logger.Log("Retrieved services");
            return Task.CompletedTask;
        }

        protected override async Task ProcessExecute() {
            try {
                await githubService.MergeBranch("release", "dev", $"Release {Build.version}");

                // Necessary to get around branch protection rules for master
                string modpackPath = Path.Join(GetBuildSourcesPath(), "modpack");
                GitCommand(modpackPath, "git fetch");
                GitCommand(modpackPath, "git checkout -t origin/dev");
                GitCommand(modpackPath, "git checkout dev");
                GitCommand(modpackPath, "git pull");
                GitCommand(modpackPath, "git checkout -t origin/master");
                GitCommand(modpackPath, "git checkout master");
                GitCommand(modpackPath, "git pull");
                GitCommand(modpackPath, "git merge dev");
                GitCommand(modpackPath, "git push -u origin master");
                Logger.Log("Release branch merges complete");
            } catch (Exception exception) {
                Warning($"Release branch merges failed:\n{exception}");
            }
        }
    }
}
