using System;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Interfaces.Modpack.BuildProcess.Steps;
using UKSF.Api.Models.Modpack;

namespace UKSF.Api.Services.Modpack.BuildProcess {
    public class BuildProcessorService : IBuildProcessorService {
        private readonly IBuildsService buildsService;
        private readonly IBuildStepService buildStepService;

        public BuildProcessorService(IBuildStepService buildStepService, IBuildsService buildsService) {
            this.buildStepService = buildStepService;
            this.buildsService = buildsService;
        }

        public async Task ProcessBuild(ModpackBuild build, CancellationTokenSource cancellationTokenSource) {
            await buildsService.SetBuildRunning(build);

            foreach (ModpackBuildStep buildStep in build.steps) {
                if (cancellationTokenSource.IsCancellationRequested) {
                    await buildsService.CancelBuild(build);
                    return;
                };

                IBuildStep step = buildStepService.ResolveBuildStep(buildStep.name);
                step.Init(build, buildStep, async () => await buildsService.UpdateBuildStep(build, buildStep), cancellationTokenSource);

                try {
                    await step.Start();
                    await step.Setup();
                    await step.Process();
                    await step.Teardown();
                    await step.Succeed();
                } catch (OperationCanceledException) {
                    await step.Cancel();
                    await buildsService.CancelBuild(build);
                    return;
                } catch (Exception exception) {
                    await step.Fail(exception);
                    await buildsService.FailBuild(build);
                    return;
                }
            }

            await buildsService.SucceedBuild(build);
        }
    }
}
