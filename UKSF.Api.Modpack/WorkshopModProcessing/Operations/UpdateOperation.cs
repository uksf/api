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
        var workshopMod = workshopModsContext.GetSingle(workshopModId);

        try
        {
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Updating, "Downloading...");
            await workshopModsProcessingService.DownloadWithRetries(workshopModId, cancellationToken: cancellationToken);
            logger.LogInfo($"Successfully downloaded workshop mod update {workshopModId}");
            return DownloadResult.Successful();
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning($"Update download cancelled for workshop mod {workshopModId}");
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Download cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorMessage = $"Failed to download workshop mod update {workshopModId}: {exception.Message}";
            logger.LogError(errorMessage, exception);
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, errorMessage);
            return DownloadResult.Failure(errorMessage);
        }
    }

    public async Task<CheckResult> CheckAsync(string workshopModId, CancellationToken cancellationToken = default)
    {
        var workshopMod = workshopModsContext.GetSingle(workshopModId);

        try
        {
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Updating, "Checking...");

            var workshopModPath = workshopModsProcessingService.GetWorkshopModPath(workshopMod.SteamId);
            var pbos = workshopModsProcessingService.GetModFiles(workshopModPath);

            logger.LogInfo($"Found {pbos.Count} PBOs for workshop mod update {workshopModId}");
            return CheckResult.Successful(pbos);
        }
        catch (Exception exception)
        {
            var errorMessage = $"Failed to check workshop mod update {workshopModId}: {exception.Message}";
            logger.LogError(errorMessage, exception);
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, errorMessage);
            return CheckResult.Failure(errorMessage);
        }
    }

    public async Task<UpdateResult> UpdateAsync(string workshopModId, List<string> selectedPbos, CancellationToken cancellationToken = default)
    {
        var workshopMod = workshopModsContext.GetSingle(workshopModId);

        try
        {
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Updating, "Updating...");

            var oldPbos = workshopMod.Pbos ?? [];
            if (oldPbos.Count > 0)
            {
                logger.LogInfo($"Removing {oldPbos.Count} old PBOs from workshop mod {workshopModId}");
                workshopModsProcessingService.DeletePbosFromDependencies(oldPbos);
            }

            await workshopModsProcessingService.CopyPbosToDependencies(workshopMod, selectedPbos, cancellationToken);

            workshopMod.Pbos = selectedPbos;
            workshopMod.LastUpdatedLocally = DateTime.UtcNow;
            workshopMod.Status = WorkshopModStatus.UpdatedPendingRelease;
            workshopMod.StatusMessage = "Updated pending next modpack release";
            workshopMod.ErrorMessage = null;
            await workshopModsContext.Replace(workshopMod);

            logger.LogInfo($"Successfully updated workshop mod {workshopModId} with {selectedPbos.Count} PBOs");
            return UpdateResult.Successful();
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning($"Update cancelled for workshop mod {workshopModId}");
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Update cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorMessage = $"Failed to update workshop mod {workshopModId}: {exception.Message}";
            logger.LogError(errorMessage, exception);
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
