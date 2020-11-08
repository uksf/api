using System.Threading.Tasks;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps.ReleaseSteps {
    [BuildStep(NAME)]
    public class BuildStepPublish : BuildStep {
        public const string NAME = "Publish";
        private IReleaseService releaseService;

        protected override Task SetupExecute() {
            releaseService = ServiceWrapper.Provider.GetService<IReleaseService>();
            Logger.Log("Retrieved services");
            return Task.CompletedTask;
        }

        protected override async Task ProcessExecute() {
            await releaseService.PublishRelease(Build.version);
            Logger.Log("Release published");
        }
    }
}
