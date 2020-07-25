using System;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Interfaces.Modpack.BuildProcess.Steps;
using UKSF.Api.Models.Game;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Services.Modpack.BuildProcess.Steps.Common;
using UKSF.Api.Services.Modpack.BuildProcess.Steps.Release;

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
                }

                IBuildStep step = buildStepService.ResolveBuildStep(buildStep.name);
                step.Init(build, buildStep, async () => await buildsService.UpdateBuildStep(build, buildStep), cancellationTokenSource);

                try {
                    await step.Start();
                    if (!await step.CheckGuards()) {
                        await step.Skip();
                        continue;
                    }

                    await step.Setup();
                    await step.Process();
                    await step.Teardown();
                    await step.Succeed();
                } catch (OperationCanceledException) {
                    await step.Cancel();
                    await ProcessRestore(step, build);
                    await buildsService.CancelBuild(build);
                    return;
                } catch (Exception exception) {
                    await step.Fail(exception);
                    await ProcessRestore(step, build);
                    await buildsService.FailBuild(build);
                    return;
                }
            }

            await buildsService.SucceedBuild(build);
        }

        private async Task ProcessRestore(IBuildStep runningStep, ModpackBuild build) {
            if (build.environment != GameEnvironment.RELEASE || runningStep is BuildStepClean || runningStep is BuildStepBackup) return;

            ModpackBuildStep restoreStep = buildStepService.GetRestoreStepForRelease();
            if (restoreStep == null) return;

            async Task UpdateCallback() {
                await buildsService.UpdateBuildStep(build, restoreStep);
            }

            restoreStep.index = build.steps.Count;
            IBuildStep step = buildStepService.ResolveBuildStep(restoreStep.name);
            step.Init(build, restoreStep, UpdateCallback, new CancellationTokenSource());
            build.steps.Add(restoreStep);
            await UpdateCallback();

            try {
                await step.Start();
                if (!await step.CheckGuards()) {
                    await step.Skip();
                } else {
                    await step.Setup();
                    await step.Process();
                    await step.Teardown();
                    await step.Succeed();
                }
            } catch (Exception exception) {
                await step.Fail(exception);
            }
        }
    }
}
