using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Operations;

public abstract class WorkshopModOperationBase(
    IWorkshopModsContext workshopModsContext,
    IWorkshopModsProcessingService workshopModsProcessingService,
    IWorkshopModDependencyFilesService workshopModDependencyFilesService,
    IWorkshopModRootFilesService workshopModRootFilesService
) : IModOperation
{
    protected readonly IWorkshopModsContext WorkshopModsContext = workshopModsContext;
    protected readonly IWorkshopModsProcessingService WorkshopModsProcessingService = workshopModsProcessingService;
    protected readonly IWorkshopModDependencyFilesService WorkshopModDependencyFilesService = workshopModDependencyFilesService;
    protected readonly IWorkshopModRootFilesService WorkshopModRootFilesService = workshopModRootFilesService;

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
            var pbos = WorkshopModsProcessingService.GetPboFiles(workshopModPath);
            var extensions = WorkshopModsProcessingService.GetExtensions(workshopModPath);
            if (pbos.Count == 0 && extensions.Count == 0)
            {
                throw new InvalidOperationException($"No PBOs or extensions found in {workshopModPath}");
            }

            var contentChanged = HasChanged(workshopMod.Pbos, pbos) || HasChanged(workshopMod.Extensions, extensions);

            if (contentChanged)
            {
                await WorkshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.InterventionRequired, "Select files to install");
            }

            await WorkshopModsProcessingService.SetAvailable(workshopMod, pbos, extensions);
            return OperationResult.Successful(interventionRequired: contentChanged, availablePbos: pbos, availableExtensions: extensions);
        }
        catch (Exception exception)
        {
            return OperationResult.Failure(exception.Message);
        }
    }

    private static bool HasChanged(List<string> installed, List<string> available)
    {
        return !(installed ?? []).OrderBy(x => x).SequenceEqual(available.OrderBy(x => x));
    }

    protected bool ExecutionFilesChanged { get; set; } = true;

    public async Task<OperationResult> ExecuteAsync(
        string workshopModId,
        List<string> selectedPbos,
        List<string> selectedExtensions,
        CancellationToken cancellationToken = default
    )
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
            await ExecuteCoreAsync(workshopMod, selectedPbos ?? [], selectedExtensions ?? [], cancellationToken);
            ApplyCompletedState(workshopMod);
            await PersistCompletedAsync(workshopMod);

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

    /// <summary>
    ///     Removes every file of this mod that is not selected: its previous selection, and any file of the workshop item that the
    ///     dependencies folder holds without a record, such as a copy made by hand before the mod was managed here. A file that any
    ///     other mod lists as its own is never removed.
    /// </summary>
    protected void DeleteUnselectedFiles(DomainWorkshopMod workshopMod, List<string> selectedPbos, List<string> selectedExtensions)
    {
        var otherMods = WorkshopModsContext.Get(x => x.Id != workshopMod.Id).ToList();
        var otherPbos = otherMods.SelectMany(x => (x.Pbos ?? []).Concat(x.AvailablePbos ?? [])).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var otherExtensions = otherMods.SelectMany(x => (x.Extensions ?? []).Concat(x.AvailableExtensions ?? [])).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var workshopModPath = WorkshopModsProcessingService.GetWorkshopModPath(workshopMod.SteamId);
        var pbosToDelete = Unselected(workshopMod.Pbos, WorkshopModsProcessingService.GetPboFiles(workshopModPath), selectedPbos, otherPbos);
        if (pbosToDelete.Count > 0)
        {
            WorkshopModDependencyFilesService.DeletePbosFromDependencies(pbosToDelete);
        }

        var extensionsToDelete = Unselected(
            workshopMod.Extensions,
            WorkshopModsProcessingService.GetExtensions(workshopModPath),
            selectedExtensions,
            otherExtensions
        );
        if (extensionsToDelete.Count > 0)
        {
            WorkshopModDependencyFilesService.DeleteExtensionsFromDependencies(extensionsToDelete);
        }
    }

    private static List<string> Unselected(List<string> previous, List<string> itemFiles, List<string> selected, HashSet<string> ownedByOthers)
    {
        return (previous ?? []).Concat(itemFiles ?? [])
                               .Distinct(StringComparer.OrdinalIgnoreCase)
                               .Except(selected, StringComparer.OrdinalIgnoreCase)
                               .Where(x => !ownedByOthers.Contains(x))
                               .ToList();
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

    protected virtual Task PersistCompletedAsync(DomainWorkshopMod workshopMod) => WorkshopModsContext.Replace(workshopMod);

    protected abstract Task ExecuteCoreAsync(
        DomainWorkshopMod workshopMod,
        List<string> selectedPbos,
        List<string> selectedExtensions,
        CancellationToken cancellationToken
    );
}
