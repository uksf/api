using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Operations;

public interface IUpdateOperation
{
    Task<DownloadResult> DownloadAsync(string workshopModId, CancellationToken cancellationToken = default);
    Task<CheckResult> CheckAsync(string workshopModId, CancellationToken cancellationToken = default);
    Task<UpdateResult> UpdateAsync(string workshopModId, List<string> selectedPbos, CancellationToken cancellationToken = default);
}

public class UpdateOperation(IWorkshopModsContext workshopModsContext, IWorkshopModsProcessingService workshopModsProcessingService, IUksfLogger logger)
    : IUpdateOperation
{
    public async Task<DownloadResult> DownloadAsync(string workshopModId, CancellationToken cancellationToken = default)
    {
        var workshopMod = workshopModsContext.GetSingle(x => x.SteamId == workshopModId);

        try
        {
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Updating, "Downloading...");
            await workshopModsProcessingService.DownloadWithRetries(workshopModId, cancellationToken: cancellationToken);
            logger.LogInfo($"Successfully downloaded workshop mod update {workshopModId}");
            return DownloadResult.Successful();
        }
        catch (OperationCanceledException)
        {
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Download cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorMessage = $"Failed to download workshop mod update {workshopModId}: {exception.Message}";
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, errorMessage);
            return DownloadResult.Failure(errorMessage);
        }
    }

    public async Task<CheckResult> CheckAsync(string workshopModId, CancellationToken cancellationToken = default)
    {
        var workshopMod = workshopModsContext.GetSingle(x => x.SteamId == workshopModId);

        if (workshopMod.RootMod)
        {
            logger.LogInfo($"Root mod {workshopModId} - skipping PBO selection for update");
            return CheckResult.Successful(interventionRequired: false);
        }

        try
        {
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Updating, "Checking...");

            var workshopModPath = workshopModsProcessingService.GetWorkshopModPath(workshopMod.SteamId);
            var currentPbos = workshopMod.Pbos ?? [];
            var pbos = workshopModsProcessingService.GetModFiles(workshopModPath);
            var pbosChanged = !currentPbos.OrderBy(x => x).SequenceEqual(pbos.OrderBy(x => x));

            logger.LogInfo($"Found {pbos.Count} PBOs for workshop mod update {workshopModId}");
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.InterventionRequired, "Select PBOs to install");
            await workshopModsProcessingService.SetAvailablePbos(workshopMod, pbos);
            return CheckResult.Successful(pbosChanged);
        }
        catch (Exception exception)
        {
            var errorMessage = $"Failed to check workshop mod update {workshopModId}: {exception.Message}";
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, errorMessage);
            return CheckResult.Failure(errorMessage);
        }
    }

    public async Task<UpdateResult> UpdateAsync(string workshopModId, List<string> selectedPbos, CancellationToken cancellationToken = default)
    {
        var workshopMod = workshopModsContext.GetSingle(x => x.SteamId == workshopModId);

        try
        {
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Updating, "Updating...");

            if (workshopMod.RootMod)
            {
                // Delete then copy for root mods - we need a clean slate to avoid stale files
                // This is safe because we only get here after successful download
                workshopModsProcessingService.DeleteRootModFromRepos(workshopMod);
                await workshopModsProcessingService.CopyRootModToRepos(workshopMod, cancellationToken);
            }
            else
            {
                // Copy new PBOs first
                await workshopModsProcessingService.CopyPbosToDependencies(workshopMod, selectedPbos, cancellationToken);

                // Then delete only the old PBOs that are no longer in the new set
                var oldPbos = workshopMod.Pbos ?? [];
                var pbosToDelete = oldPbos.Except(selectedPbos, StringComparer.OrdinalIgnoreCase).ToList();
                if (pbosToDelete.Count > 0)
                {
                    logger.LogInfo($"Removing {pbosToDelete.Count} old PBOs no longer in workshop mod {workshopModId}");
                    workshopModsProcessingService.DeletePbosFromDependencies(pbosToDelete);
                }

                workshopMod.Pbos = selectedPbos;
            }

            workshopMod.LastUpdatedLocally = DateTime.UtcNow;
            workshopMod.Status = WorkshopModStatus.UpdatedPendingRelease;
            workshopMod.StatusMessage = "Updated pending next modpack release";
            workshopMod.ErrorMessage = null;
            await workshopModsContext.Replace(workshopMod);

            if (workshopMod.RootMod)
            {
                logger.LogInfo($"Successfully updated root mod {workshopModId}");
            }
            else
            {
                logger.LogInfo($"Successfully updated workshop mod {workshopModId} with {selectedPbos.Count} PBOs");
            }

            return UpdateResult.Successful();
        }
        catch (OperationCanceledException)
        {
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Update cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorMessage = $"Failed to update workshop mod {workshopModId}: {exception.Message}";
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, errorMessage);
            return UpdateResult.Failure(errorMessage);
        }
    }
}

public record UpdateResult(bool Success, string ErrorMessage = null)
{
    public static UpdateResult Successful()
    {
        return new UpdateResult(true);
    }

    public static UpdateResult Failure(string errorMessage)
    {
        return new UpdateResult(false, errorMessage);
    }
}
