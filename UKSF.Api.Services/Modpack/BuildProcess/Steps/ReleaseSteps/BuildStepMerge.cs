using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Interfaces.Integrations.Github;
using UKSF.Api.Services.Common;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps.ReleaseSteps {
    [BuildStep(NAME)]
    public class BuildStepMerge : BuildStep {
        public const string NAME = "Merge";
        private IGithubService githubService;

        protected override Task SetupExecute() {
            githubService = ServiceWrapper.Provider.GetService<IGithubService>();
            Logger.Log("Retrieved services");
            return Task.CompletedTask;
        }

        protected override async Task ProcessExecute() {
            try {
                await githubService.MergeBranch("dev", "release", $"Release {Build.version}");
                await githubService.MergeBranch("master", "release", $"Release {Build.version}");
                Logger.Log("Release branch merges complete");
            } catch (Exception exception) {
                Warning($"Release branch merges failed:\n{exception}");
            }
        }
    }
}
