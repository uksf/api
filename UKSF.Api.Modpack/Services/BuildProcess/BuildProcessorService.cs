using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services.BuildProcess.Steps;
using UKSF.Api.Modpack.Services.BuildProcess.Steps.Common;
using UKSF.Api.Modpack.Services.BuildProcess.Steps.ReleaseSteps;

namespace UKSF.Api.Modpack.Services.BuildProcess;

public interface IBuildProcessorService
{
    Task ProcessBuildWithErrorHandling(ModpackBuild build, CancellationTokenSource cancellationTokenSource);
}

public class BuildProcessorService : IBuildProcessorService
{
    private readonly IBuildsService _buildsService;
    private readonly IBuildStepService _buildStepService;
    private readonly IUksfLogger _logger;
    private readonly IServiceProvider _serviceProvider;

    public BuildProcessorService(IServiceProvider serviceProvider, IBuildStepService buildStepService, IBuildsService buildsService, IUksfLogger logger)
    {
        _serviceProvider = serviceProvider;
        _buildStepService = buildStepService;
        _buildsService = buildsService;
        _logger = logger;
    }

    public async Task ProcessBuildWithErrorHandling(ModpackBuild build, CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            await ProcessBuild(build, cancellationTokenSource);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception);
            await _buildsService.FailBuild(build);
        }
    }

    private async Task ProcessBuild(ModpackBuild build, CancellationTokenSource cancellationTokenSource)
    {
        await _buildsService.SetBuildRunning(build);

        foreach (var buildStep in build.Steps)
        {
            var step = _buildStepService.ResolveBuildStep(buildStep.Name);
            step.Init(
                _serviceProvider,
                build,
                buildStep,
                updateDefinition => _buildsService.UpdateBuild(build, updateDefinition),
                () => _buildsService.UpdateBuildStep(build, buildStep),
                cancellationTokenSource
            );

            if (cancellationTokenSource.IsCancellationRequested)
            {
                await step.Cancel();
                await _buildsService.CancelBuild(build);
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
            catch (OperationCanceledException)
            {
                await step.Cancel();
                await ProcessRestore(step, build);
                await _buildsService.CancelBuild(build);
                return;
            }
            catch (Exception exception)
            {
                await step.Fail(exception);
                await ProcessRestore(step, build);
                await _buildsService.FailBuild(build);
                return;
            }
        }

        await _buildsService.SucceedBuild(build);
    }

    private async Task ProcessRestore(IBuildStep runningStep, ModpackBuild build)
    {
        if (build.Environment != GameEnvironment.RELEASE || runningStep is BuildStepClean || runningStep is BuildStepBackup)
        {
            return;
        }

        _logger.LogInfo($"Attempting to restore repo prior to {build.Version}");
        var restoreSteps = _buildStepService.GetStepsForReleaseRestore();
        if (restoreSteps.Any())
        {
            _logger.LogError("Restore steps expected but not found. Won't restore");
            return;
        }

        var lastStepIndex = build.Steps.Last().Index;
        foreach (var restoreStep in restoreSteps)
        {
            restoreStep.Index = ++lastStepIndex;
            await ExecuteRestoreStep(build, restoreStep);
        }
    }

    private async Task ExecuteRestoreStep(ModpackBuild build, ModpackBuildStep restoreStep)
    {
        var step = _buildStepService.ResolveBuildStep(restoreStep.Name);
        step.Init(
            _serviceProvider,
            build,
            restoreStep,
            async updateDefinition => await _buildsService.UpdateBuild(build, updateDefinition),
            async () => await _buildsService.UpdateBuildStep(build, restoreStep),
            new()
        );
        build.Steps.Add(restoreStep);
        await _buildsService.UpdateBuildStep(build, restoreStep);

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
