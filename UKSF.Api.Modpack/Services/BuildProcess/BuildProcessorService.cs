using System;
using System.Threading;
using System.Threading.Tasks;
using UKSF.Api.Admin.Services;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Base.Events;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services.BuildProcess.Steps;
using UKSF.Api.Modpack.Services.BuildProcess.Steps.Common;
using UKSF.Api.Modpack.Services.BuildProcess.Steps.ReleaseSteps;

namespace UKSF.Api.Modpack.Services.BuildProcess {
    public interface IBuildProcessorService {
        Task ProcessBuild(ModpackBuild build, CancellationTokenSource cancellationTokenSource);
    }

    public class BuildProcessorService : IBuildProcessorService {
        private readonly IBuildsService buildsService;
        private readonly IBuildStepService buildStepService;
        private readonly IVariablesService variablesService;
        private readonly ILogger logger;

        public BuildProcessorService(IBuildStepService buildStepService, IBuildsService buildsService, IVariablesService variablesService, ILogger logger) {
            this.buildStepService = buildStepService;
            this.buildsService = buildsService;
            this.variablesService = variablesService;
            this.logger = logger;
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
                    cancellationTokenSource,
                    variablesService
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
            logger.LogInfo($"Attempting to restore repo prior to {build.version}");
            if (build.environment != GameEnvironment.RELEASE || runningStep is BuildStepClean || runningStep is BuildStepBackup) {
                return;
            }

            ModpackBuildStep restoreStep = buildStepService.GetRestoreStepForRelease();
            if (restoreStep == null) {
                logger.LogError("Restore step expected but not found. Won't restore");
                return;
            }

            restoreStep.index = build.steps.Count;
            IBuildStep step = buildStepService.ResolveBuildStep(restoreStep.name);
            step.Init(
                build,
                restoreStep,
                async updateDefinition => await buildsService.UpdateBuild(build, updateDefinition),
                async () => await buildsService.UpdateBuildStep(build, restoreStep),
                new CancellationTokenSource(),
                variablesService
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
