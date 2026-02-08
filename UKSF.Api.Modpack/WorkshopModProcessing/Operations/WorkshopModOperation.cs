using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Operations;

public interface IWorkshopModOperation
{
    Task<OperationResult> DownloadAsync(string workshopModId, WorkshopModOperationType type, CancellationToken cancellationToken = default);
    Task<OperationResult> CheckAsync(string workshopModId, WorkshopModOperationType type, CancellationToken cancellationToken = default);

    Task<OperationResult> ExecuteAsync(
        string workshopModId,
        WorkshopModOperationType type,
        List<string> selectedPbos,
        CancellationToken cancellationToken = default
    );
}

public class WorkshopModOperation(IWorkshopModsContext workshopModsContext, IWorkshopModsProcessingService workshopModsProcessingService, IUksfLogger logger)
    : IWorkshopModOperation
{
    public async Task<OperationResult> DownloadAsync(string workshopModId, WorkshopModOperationType type, CancellationToken cancellationToken = default)
    {
        var workshopMod = workshopModsContext.GetSingle(x => x.SteamId == workshopModId);
        if (workshopMod == null)
        {
            return OperationResult.Failure($"Workshop mod {workshopModId} not found");
        }

        var activeStatus = type == WorkshopModOperationType.Install ? WorkshopModStatus.Installing : WorkshopModStatus.Updating;
        var label = type == WorkshopModOperationType.Install ? "download" : "update download";

        try
        {
            await workshopModsProcessingService.UpdateModStatus(workshopMod, activeStatus, "Downloading...");
            await workshopModsProcessingService.DownloadWithRetries(workshopModId, cancellationToken: cancellationToken);
            logger.LogInfo($"Successfully downloaded workshop mod {label} {workshopModId}");
            return OperationResult.Successful();
        }
        catch (OperationCanceledException)
        {
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Download cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorMessage = $"Failed to {label} workshop mod {workshopModId}: {exception.Message}";
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, errorMessage);
            return OperationResult.Failure(errorMessage);
        }
    }

    public async Task<OperationResult> CheckAsync(string workshopModId, WorkshopModOperationType type, CancellationToken cancellationToken = default)
    {
        var workshopMod = workshopModsContext.GetSingle(x => x.SteamId == workshopModId);
        if (workshopMod == null)
        {
            return OperationResult.Failure($"Workshop mod {workshopModId} not found");
        }

        if (workshopMod.RootMod)
        {
            var label = type == WorkshopModOperationType.Install ? "" : " for update";
            logger.LogInfo($"Root mod {workshopModId} - skipping PBO selection{label}");
            return OperationResult.Successful(interventionRequired: false);
        }

        var activeStatus = type == WorkshopModOperationType.Install ? WorkshopModStatus.Installing : WorkshopModStatus.Updating;

        try
        {
            await workshopModsProcessingService.UpdateModStatus(workshopMod, activeStatus, "Checking...");

            var workshopModPath = workshopModsProcessingService.GetWorkshopModPath(workshopMod.SteamId);
            var currentPbos = workshopMod.Pbos ?? [];
            var pbos = workshopModsProcessingService.GetModFiles(workshopModPath);
            var pbosChanged = !currentPbos.OrderBy(x => x).SequenceEqual(pbos.OrderBy(x => x));

            var typeLabel = type == WorkshopModOperationType.Install ? "" : " update";
            logger.LogInfo($"Found {pbos.Count} PBOs for workshop mod{typeLabel} {workshopModId}");

            if (pbosChanged)
            {
                await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.InterventionRequired, "Select PBOs to install");
            }

            await workshopModsProcessingService.SetAvailablePbos(workshopMod, pbos);
            return OperationResult.Successful(interventionRequired: pbosChanged, availablePbos: pbos);
        }
        catch (Exception exception)
        {
            var typeLabel = type == WorkshopModOperationType.Install ? "" : " update";
            var errorMessage = $"Failed to check workshop mod{typeLabel} {workshopModId}: {exception.Message}";
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, errorMessage);
            return OperationResult.Failure(errorMessage);
        }
    }

    public async Task<OperationResult> ExecuteAsync(
        string workshopModId,
        WorkshopModOperationType type,
        List<string> selectedPbos,
        CancellationToken cancellationToken = default
    )
    {
        var workshopMod = workshopModsContext.GetSingle(x => x.SteamId == workshopModId);
        if (workshopMod == null)
        {
            return OperationResult.Failure($"Workshop mod {workshopModId} not found");
        }

        var activeStatus = type == WorkshopModOperationType.Install ? WorkshopModStatus.Installing : WorkshopModStatus.Updating;
        var actionVerb = type == WorkshopModOperationType.Install ? "install" : "update";
        var pastVerb = type == WorkshopModOperationType.Install ? "installed" : "updated";

        try
        {
            await workshopModsProcessingService.UpdateModStatus(workshopMod, activeStatus, $"{char.ToUpper(actionVerb[0])}{actionVerb[1..]}ing...");

            if (type == WorkshopModOperationType.Install)
            {
                if (workshopMod.RootMod)
                {
                    await workshopModsProcessingService.CopyRootModToRepos(workshopMod, cancellationToken);
                }
                else
                {
                    await workshopModsProcessingService.CopyPbosToDependencies(workshopMod, selectedPbos, cancellationToken);
                    workshopMod.Pbos = selectedPbos;
                }

                workshopMod.Status = WorkshopModStatus.InstalledPendingRelease;
                workshopMod.StatusMessage = "Installed pending next modpack release";
            }
            else
            {
                if (workshopMod.RootMod)
                {
                    workshopModsProcessingService.DeleteRootModFromRepos(workshopMod);
                    await workshopModsProcessingService.CopyRootModToRepos(workshopMod, cancellationToken);
                }
                else
                {
                    await workshopModsProcessingService.CopyPbosToDependencies(workshopMod, selectedPbos, cancellationToken);

                    var oldPbos = workshopMod.Pbos ?? [];
                    var pbosToDelete = oldPbos.Except(selectedPbos, StringComparer.OrdinalIgnoreCase).ToList();
                    if (pbosToDelete.Count > 0)
                    {
                        logger.LogInfo($"Removing {pbosToDelete.Count} old PBOs no longer in workshop mod {workshopModId}");
                        workshopModsProcessingService.DeletePbosFromDependencies(pbosToDelete);
                    }

                    workshopMod.Pbos = selectedPbos;
                }

                workshopMod.Status = WorkshopModStatus.UpdatedPendingRelease;
                workshopMod.StatusMessage = "Updated pending next modpack release";
            }

            workshopMod.LastUpdatedLocally = DateTime.UtcNow;
            workshopMod.ErrorMessage = null;
            await workshopModsContext.Replace(workshopMod);

            if (workshopMod.RootMod)
            {
                logger.LogInfo($"Successfully {pastVerb} root mod {workshopModId}");
            }
            else
            {
                logger.LogInfo($"Successfully {pastVerb} workshop mod {workshopModId} with {selectedPbos.Count} PBOs");
            }

            return OperationResult.Successful();
        }
        catch (OperationCanceledException)
        {
            await workshopModsProcessingService.UpdateModStatus(
                workshopMod,
                WorkshopModStatus.Error,
                $"{char.ToUpper(actionVerb[0])}{actionVerb[1..]} cancelled"
            );
            throw;
        }
        catch (Exception exception)
        {
            var errorMessage = $"Failed to {actionVerb} workshop mod {workshopModId}: {exception.Message}";
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, errorMessage);
            return OperationResult.Failure(errorMessage);
        }
    }
}
