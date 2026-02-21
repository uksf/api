using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Operations;

public abstract class WorkshopModOperationBase(IWorkshopModsContext workshopModsContext, IWorkshopModsProcessingService workshopModsProcessingService)
    : IModOperation
{
    protected readonly IWorkshopModsContext WorkshopModsContext = workshopModsContext;
    protected readonly IWorkshopModsProcessingService WorkshopModsProcessingService = workshopModsProcessingService;

    protected abstract WorkshopModStatus ActiveStatus { get; }
    protected abstract string CancelPrefix { get; }
    protected abstract WorkshopModStatus CompletedStatus { get; }
    protected abstract string CompletedMessage { get; }
    protected abstract string ActiveStatusMessage { get; }

    private DomainWorkshopMod GetMod(string workshopModId) => WorkshopModsContext.GetSingle(x => x.SteamId == workshopModId);

    public async Task<OperationResult> DownloadAsync(string workshopModId, CancellationToken cancellationToken = default)
    {
        var workshopMod = GetMod(workshopModId);
        if (workshopMod == null)
        {
            return OperationResult.Failure($"Workshop mod {workshopModId} not found");
        }

        try
        {
            await WorkshopModsProcessingService.UpdateModStatus(workshopMod, ActiveStatus, "Downloading...");
            await WorkshopModsProcessingService.DownloadWithRetries(workshopModId, cancellationToken: cancellationToken);
            return OperationResult.Successful();
        }
        catch (OperationCanceledException)
        {
            await WorkshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, $"{CancelPrefix} cancelled");
            throw;
        }
        catch (Exception exception)
        {
            return OperationResult.Failure(exception.Message);
        }
    }

    public async Task<OperationResult> CheckAsync(string workshopModId, CancellationToken cancellationToken = default)
    {
        var workshopMod = GetMod(workshopModId);
        if (workshopMod == null)
        {
            return OperationResult.Failure($"Workshop mod {workshopModId} not found");
        }

        if (workshopMod.RootMod)
        {
            return OperationResult.Successful(interventionRequired: false);
        }

        try
        {
            await WorkshopModsProcessingService.UpdateModStatus(workshopMod, ActiveStatus, "Checking...");

            var workshopModPath = WorkshopModsProcessingService.GetWorkshopModPath(workshopMod.SteamId);
            var currentPbos = workshopMod.Pbos ?? [];
            var pbos = WorkshopModsProcessingService.GetModFiles(workshopModPath);
            var pbosChanged = !currentPbos.OrderBy(x => x).SequenceEqual(pbos.OrderBy(x => x));

            if (pbosChanged)
            {
                await WorkshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.InterventionRequired, "Select PBOs to install");
            }

            await WorkshopModsProcessingService.SetAvailablePbos(workshopMod, pbos);
            return OperationResult.Successful(interventionRequired: pbosChanged, availablePbos: pbos);
        }
        catch (Exception exception)
        {
            return OperationResult.Failure(exception.Message);
        }
    }

    protected bool ExecutionFilesChanged { get; set; } = true;

    public async Task<OperationResult> ExecuteAsync(string workshopModId, List<string> selectedPbos, CancellationToken cancellationToken = default)
    {
        var workshopMod = GetMod(workshopModId);
        if (workshopMod == null)
        {
            return OperationResult.Failure($"Workshop mod {workshopModId} not found");
        }

        ExecutionFilesChanged = true;
        OnBeforeExecute(workshopMod);

        var skipResult = ShouldSkipExecution(workshopMod);
        if (skipResult != null)
        {
            return skipResult;
        }

        try
        {
            await WorkshopModsProcessingService.UpdateModStatus(workshopMod, ActiveStatus, ActiveStatusMessage);
            await ExecuteCoreAsync(workshopMod, selectedPbos, cancellationToken);
            ApplyCompletedState(workshopMod);
            await WorkshopModsContext.Replace(workshopMod);

            return OperationResult.Successful(filesChanged: ExecutionFilesChanged);
        }
        catch (OperationCanceledException)
        {
            await WorkshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, $"{CancelPrefix} cancelled");
            throw;
        }
        catch (Exception exception)
        {
            return OperationResult.Failure(exception.Message);
        }
    }

    protected virtual void OnBeforeExecute(DomainWorkshopMod workshopMod) { }

    protected virtual OperationResult ShouldSkipExecution(DomainWorkshopMod workshopMod) => null;

    protected virtual void ApplyCompletedState(DomainWorkshopMod workshopMod)
    {
        workshopMod.Status = CompletedStatus;
        workshopMod.StatusMessage = CompletedMessage;
        workshopMod.LastUpdatedLocally = DateTime.UtcNow;
        workshopMod.ErrorMessage = null;
    }

    protected abstract Task ExecuteCoreAsync(DomainWorkshopMod workshopMod, List<string> selectedPbos, CancellationToken cancellationToken);
}
