using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Operations;

public interface IUninstallOperation
{
    Task<UninstallResult> UninstallAsync(string workshopModId, CancellationToken cancellationToken = default);
}

public class UninstallOperation(IWorkshopModsContext workshopModsContext, IWorkshopModsProcessingService workshopModsProcessingService, IUksfLogger logger)
    : IUninstallOperation
{
    public async Task<UninstallResult> UninstallAsync(string workshopModId, CancellationToken cancellationToken = default)
    {
        var workshopMod = workshopModsContext.GetSingle(workshopModId);

        if (workshopMod.Status is WorkshopModStatus.Uninstalled or WorkshopModStatus.UninstalledPendingRelease)
        {
            logger.LogWarning($"Workshop mod {workshopModId} is already uninstalled");
            return UninstallResult.Successful();
        }

        try
        {
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Uninstalling, "Uninstalling...");

            var pbosToDelete = workshopMod.Pbos ?? [];
            if (pbosToDelete.Count > 0)
            {
                logger.LogInfo($"Removing {pbosToDelete.Count} PBOs from workshop mod {workshopModId}");
                workshopModsProcessingService.DeletePbosFromDependencies(pbosToDelete);
            }

            workshopMod.Status = WorkshopModStatus.UninstalledPendingRelease;
            workshopMod.Pbos = [];
            workshopMod.StatusMessage = "Uninstalled pending next modpack release";
            workshopMod.ErrorMessage = null;
            await workshopModsContext.Replace(workshopMod);

            logger.LogInfo($"Successfully uninstalled workshop mod {workshopModId}");
            return UninstallResult.Successful();
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning($"Uninstall cancelled for workshop mod {workshopModId}");
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Uninstall cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorMessage = $"Failed to uninstall workshop mod {workshopModId}: {exception.Message}";
            logger.LogError(errorMessage, exception);
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, errorMessage);
            return UninstallResult.Failure(errorMessage);
        }
    }
}

public record UninstallResult(bool Success, string ErrorMessage = null)
{
    public static UninstallResult Successful()
    {
        return new UninstallResult(true);
    }

    public static UninstallResult Failure(string errorMessage)
    {
        return new UninstallResult(false, errorMessage);
    }
}
