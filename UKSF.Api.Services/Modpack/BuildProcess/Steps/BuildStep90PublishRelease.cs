using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Services.Common;

namespace UKSF.Api.Services.Modpack.BuildProcess.Steps {
    public class BuildStep90PublishRelease : BuildStep {
        public const string NAME = "Publish";
        private IReleaseService releaseService;

        public override async Task<bool> CheckGuards() {
            await Logger.Log("\nChecking step guards", COLOUR_BLUE);
            return await ReleaseBuildGuard();
        }

        protected override async Task SetupExecute() {
            releaseService = ServiceWrapper.Provider.GetService<IReleaseService>();
            await Logger.Log("Retrieved services");
        }

        protected override async Task ProcessExecute() {
            await releaseService.PublishRelease(Build.version);
            await Logger.Log("Release published");
        }
    }
}
