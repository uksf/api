using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Modpack.BuildProcess.Steps;
using UKSF.Api.Modpack.BuildProcess.Steps.Common;
using UKSF.Api.Modpack.BuildProcess.Steps.ReleaseSteps;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.BuildProcess;

public interface IBuildProcessorService
{
    Task ProcessBuildWithErrorHandling(DomainModpackBuild build, CancellationTokenSource cancellationTokenSource);
}

public class BuildProcessorService(IServiceProvider serviceProvider, IBuildStepService buildStepService, IBuildsService buildsService, IUksfLogger logger)
    : IBuildProcessorService
{
    public async Task ProcessBuildWithErrorHandling(DomainModpackBuild build, CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            await ProcessBuild(build, cancellationTokenSource);
        }
        catch (Exception exception)
        {
            logger.LogError(exception);
            await buildsService.FailBuild(build);
        }
    }

    private async Task ProcessBuild(DomainModpackBuild build, CancellationTokenSource cancellationTokenSource)
    {
        await buildsService.SetBuildRunning(build);

        foreach (var buildStep in build.Steps)
        {
            var step = buildStepService.ResolveBuildStep(buildStep.Name);
            step.Init(
                serviceProvider,
                logger,
                build,
                buildStep,
                updateDefinition => buildsService.UpdateBuild(build, updateDefinition),
                () => buildsService.UpdateBuildStep(build, buildStep),
                cancellationTokenSource
            );

            if (cancellationTokenSource.IsCancellationRequested)
            {
                await step.Cancel();
                await buildsService.UpdateBuildStep(build, buildStep);
                await buildsService.CancelBuild(build);
                return;
            }

            try
            {
                await step.Start();
                if (!step.CheckGuards())
                {
                    await step.Skip();
                    continue;
                }

                await step.Setup();
                await step.Process();
                await step.Succeed();
            }
            catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
            {
                await step.Cancel();
                await buildsService.UpdateBuildStep(build, buildStep);
                await ProcessRestore(step, build);
                await buildsService.CancelBuild(build);
                return;
            }
            catch (Exception exception)
            {
                await step.Fail(exception);
                await ProcessRestore(step, build);
                await buildsService.FailBuild(build);
                return;
            }
        }

        await buildsService.SucceedBuild(build);
    }

    private async Task ProcessRestore(IBuildStep runningStep, DomainModpackBuild build)
    {
        if (build.Environment != GameEnvironment.Release || runningStep is BuildStepClean || runningStep is BuildStepBackup)
        {
            return;
        }

        logger.LogInfo($"Attempting to restore repo prior to {build.Version}");
        var restoreSteps = buildStepService.GetStepsForReleaseRestore();
        if (!restoreSteps.Any())
        {
            logger.LogError("Restore steps expected but not found. Won't restore");
            return;
        }

        var lastStepIndex = build.Steps.Last().Index;
        foreach (var restoreStep in restoreSteps)
        {
            restoreStep.Index = ++lastStepIndex;
            await ExecuteRestoreStep(build, restoreStep);
        }
    }

    private async Task ExecuteRestoreStep(DomainModpackBuild build, ModpackBuildStep restoreStep)
    {
        var step = buildStepService.ResolveBuildStep(restoreStep.Name);
        step.Init(
            serviceProvider,
            logger,
            build,
            restoreStep,
            async updateDefinition => await buildsService.UpdateBuild(build, updateDefinition),
            async () => await buildsService.UpdateBuildStep(build, restoreStep),
            new CancellationTokenSource()
        );
        build.Steps.Add(restoreStep);
        await buildsService.UpdateBuildStep(build, restoreStep);

        try
        {
            await step.Start();
            if (!step.CheckGuards())
            {
                await step.Skip();
            }
            else
            {
                await step.Setup();
                await step.Process();
                await step.Succeed();
            }
        }
        catch (Exception exception)
        {
            await step.Fail(exception);
        }
    }
}
