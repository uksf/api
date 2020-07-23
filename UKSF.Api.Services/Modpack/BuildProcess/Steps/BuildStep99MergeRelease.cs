using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Interfaces.Integrations.Github;
using UKSF.Api.Services.Common;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps {
    public class BuildStep99MergeRelease : BuildStep {
        public const string NAME = "Merge";
        private IGithubService githubService;

        protected override async Task SetupExecute() {
            githubService = ServiceWrapper.Provider.GetService<IGithubService>();
            await Logger.Log("Retrieved services");
        }

        protected override async Task ProcessExecute() {
            try {
                await githubService.MergeBranch("dev", "release", $"Release {Build.version}");
                await githubService.MergeBranch("master", "dev", $"Release {Build.version}");
                await Logger.Log("Release branch merges complete");
            } catch (Exception exception) {
                await Logger.LogWarning($"Release branch merges failed:\n\n{exception}");
            }
        }
    }
}
