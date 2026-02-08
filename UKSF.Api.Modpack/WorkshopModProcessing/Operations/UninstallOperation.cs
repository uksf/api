using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Operations;

public interface IUninstallOperation
{
    Task<OperationResult> UninstallAsync(string workshopModId, CancellationToken cancellationToken = default);
}

public class UninstallOperation(IWorkshopModsContext workshopModsContext, IWorkshopModsProcessingService workshopModsProcessingService, IUksfLogger logger)
    : IUninstallOperation
{
    public async Task<OperationResult> UninstallAsync(string workshopModId, CancellationToken cancellationToken = default)
    {
        var workshopMod = workshopModsContext.GetSingle(x => x.SteamId == workshopModId);
        if (workshopMod == null)
        {
            return OperationResult.Failure($"Workshop mod {workshopModId} not found");
        }

        if (workshopMod.Status is WorkshopModStatus.Uninstalled or WorkshopModStatus.UninstalledPendingRelease)
        {
            logger.LogWarning($"Workshop mod {workshopModId} is already uninstalled");
            return OperationResult.Successful(filesChanged: false);
        }

        try
        {
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Uninstalling, "Uninstalling...");

            var filesChanged = false;
            if (workshopMod.RootMod)
            {
                workshopModsProcessingService.DeleteRootModFromRepos(workshopMod);
                filesChanged = true;
            }
            else
            {
                var pbosToDelete = workshopMod.Pbos ?? [];
                if (pbosToDelete.Count > 0)
                {
                    logger.LogInfo($"Removing {pbosToDelete.Count} PBOs from workshop mod {workshopModId}");
                    workshopModsProcessingService.DeletePbosFromDependencies(pbosToDelete);
                    filesChanged = true;
                }
            }

            if (workshopMod.Status is WorkshopModStatus.Installed or WorkshopModStatus.InstalledPendingRelease or WorkshopModStatus.UpdatedPendingRelease)
            {
                workshopMod.Status = WorkshopModStatus.UninstalledPendingRelease;
                workshopMod.StatusMessage = "Uninstalled pending next modpack release";
            }
            else
            {
                workshopMod.Status = WorkshopModStatus.Uninstalled;
                workshopMod.StatusMessage = "Uninstalled";
            }

            workshopMod.Pbos = [];
            workshopMod.ErrorMessage = null;
            await workshopModsContext.Replace(workshopMod);

            logger.LogInfo($"Successfully uninstalled workshop mod {workshopModId}");
            return OperationResult.Successful(filesChanged: filesChanged);
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
            return OperationResult.Failure(errorMessage);
        }
    }
}
