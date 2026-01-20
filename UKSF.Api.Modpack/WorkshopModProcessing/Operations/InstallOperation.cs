using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Operations;

public interface IInstallOperation
{
    Task<DownloadResult> DownloadAsync(string workshopModId, CancellationToken cancellationToken = default);
    Task<CheckResult> CheckAsync(string workshopModId, CancellationToken cancellationToken = default);
    Task<InstallResult> InstallAsync(string workshopModId, List<string> selectedPbos, CancellationToken cancellationToken = default);
}

public class InstallOperation(IWorkshopModsContext workshopModsContext, IWorkshopModsProcessingService workshopModsProcessingService, IUksfLogger logger)
    : IInstallOperation
{
    public async Task<DownloadResult> DownloadAsync(string workshopModId, CancellationToken cancellationToken = default)
    {
        var workshopMod = workshopModsContext.GetSingle(x => x.SteamId == workshopModId);

        try
        {
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Installing, "Downloading...");
            await workshopModsProcessingService.DownloadWithRetries(workshopModId, cancellationToken: cancellationToken);
            logger.LogInfo($"Successfully downloaded workshop mod {workshopModId}");
            return DownloadResult.Successful();
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning($"Download cancelled for workshop mod {workshopModId}");
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Download cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorMessage = $"Failed to download workshop mod {workshopModId}: {exception.Message}";
            logger.LogError(errorMessage, exception);
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, errorMessage);
            return DownloadResult.Failure(errorMessage);
        }
    }

    public async Task<CheckResult> CheckAsync(string workshopModId, CancellationToken cancellationToken = default)
    {
        var workshopMod = workshopModsContext.GetSingle(x => x.SteamId == workshopModId);

        try
        {
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Installing, "Checking...");

            var workshopModPath = workshopModsProcessingService.GetWorkshopModPath(workshopMod.SteamId);
            var pbos = workshopModsProcessingService.GetModFiles(workshopModPath);

            logger.LogInfo($"Found {pbos.Count} PBOs for workshop mod {workshopModId}");
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.InterventionRequired, "Select PBOs to install");
            return CheckResult.Successful(pbos);
        }
        catch (Exception exception)
        {
            var errorMessage = $"Failed to check workshop mod {workshopModId}: {exception.Message}";
            logger.LogError(errorMessage, exception);
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, errorMessage);
            return CheckResult.Failure(errorMessage);
        }
    }

    public async Task<InstallResult> InstallAsync(string workshopModId, List<string> selectedPbos, CancellationToken cancellationToken = default)
    {
        var workshopMod = workshopModsContext.GetSingle(x => x.SteamId == workshopModId);

        try
        {
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Installing, "Installing...");
            await workshopModsProcessingService.CopyPbosToDependencies(workshopMod, selectedPbos, cancellationToken);

            workshopMod.Pbos = selectedPbos;
            workshopMod.LastUpdatedLocally = DateTime.UtcNow;
            workshopMod.Status = WorkshopModStatus.InstalledPendingRelease;
            workshopMod.StatusMessage = "Installed pending next modpack release";
            workshopMod.ErrorMessage = null;
            await workshopModsContext.Replace(workshopMod);

            logger.LogInfo($"Successfully installed workshop mod {workshopModId} with {selectedPbos.Count} PBOs");
            return InstallResult.Successful();
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning($"Installation cancelled for workshop mod {workshopModId}");
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Installation cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorMessage = $"Failed to install workshop mod {workshopModId}: {exception.Message}";
            logger.LogError(errorMessage, exception);
            await workshopModsProcessingService.UpdateModStatus(workshopMod, WorkshopModStatus.Error, errorMessage);
            return InstallResult.Failure(errorMessage);
        }
    }
}

public record DownloadResult(bool Success, string ErrorMessage = null)
{
    public static DownloadResult Successful()
    {
        return new DownloadResult(true);
    }

    public static DownloadResult Failure(string errorMessage)
    {
        return new DownloadResult(false, errorMessage);
    }
}

public record CheckResult(bool Success, List<string> AvailablePbos = null, string ErrorMessage = null)
{
    public static CheckResult Successful(List<string> availablePbos)
    {
        return new CheckResult(true, availablePbos);
    }

    public static CheckResult Failure(string errorMessage)
    {
        return new CheckResult(false, ErrorMessage: errorMessage);
    }
}

public record InstallResult(bool Success, string ErrorMessage = null)
{
    public static InstallResult Successful()
    {
        return new InstallResult(true);
    }

    public static InstallResult Failure(string errorMessage)
    {
        return new InstallResult(false, errorMessage);
    }
}
