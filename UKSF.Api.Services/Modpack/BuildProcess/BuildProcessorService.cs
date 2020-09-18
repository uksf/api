using System;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Modpack;
using UKSF.Api.Interfaces.Modpack.BuildProcess;
using UKSF.Api.Interfaces.Modpack.BuildProcess.Steps;
using UKSF.Api.Models.Game;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Services.Message;
using UKSF.Api.Services.Modpack.BuildProcess.Steps.Common;
using UKSF.Api.Services.Modpack.BuildProcess.Steps.ReleaseSteps;

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
                IBuildStep step = buildStepService.ResolveBuildStep(buildStep.name);
                step.Init(
                    build,
                    buildStep,
                    async updateDefinition => await buildsService.UpdateBuild(build, updateDefinition),
                    async () => await buildsService.UpdateBuildStep(build, buildStep),
                    cancellationTokenSource
                );

                if (cancellationTokenSource.IsCancellationRequested) {
                    await step.Cancel();
                    await buildsService.CancelBuild(build);
                    return;
                }

                try {
                    await step.Start();
                    if (!step.CheckGuards()) {
                        await step.Skip();
                        continue;
                    }

                    await step.Setup();
                    await step.Process();
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
            LogWrapper.Log($"Attempting to restore repo prior to {build.version}");
            if (build.environment != GameEnvironment.RELEASE || runningStep is BuildStepClean || runningStep is BuildStepBackup) {
                LogWrapper.Log($"Won't restore. Env: {build.environment}, Step: {runningStep.GetType().Name}");
                return;
            }

            ModpackBuildStep restoreStep = buildStepService.GetRestoreStepForRelease();
            if (restoreStep == null) {
                LogWrapper.Log($"Won't restore. Restore step not found");
                return;
            }

            restoreStep.index = build.steps.Count;
            IBuildStep step = buildStepService.ResolveBuildStep(restoreStep.name);
            step.Init(
                build,
                restoreStep,
                async updateDefinition => await buildsService.UpdateBuild(build, updateDefinition),
                async () => await buildsService.UpdateBuildStep(build, restoreStep),
                new CancellationTokenSource()
            );
            build.steps.Add(restoreStep);
            await buildsService.UpdateBuildStep(build, restoreStep);

            try {
                await step.Start();
                if (!step.CheckGuards()) {
                    await step.Skip();
                } else {
                    await step.Setup();
                    await step.Process();
                    await step.Succeed();
                }
            } catch (Exception exception) {
                await step.Fail(exception);
            }
        }
    }
}
