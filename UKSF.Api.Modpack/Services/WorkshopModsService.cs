using MassTransit;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.WorkshopModProcessing;

namespace UKSF.Api.Modpack.Services;

public interface IWorkshopModsService
{
    Task<Dictionary<string, DateTime>> GetWorkshopModUpdatedDates();
    Task InstallWorkshopMod(string workshopModId, bool rootMod, string folderName = null);
    Task UpdateWorkshopMod(string workshopModId);
    Task RetryWorkshopMod(string workshopModId);
    Task UninstallWorkshopMod(string workshopModId);
    Task DeleteWorkshopMod(string workshopModId);
    Task ResolveWorkshopModManualIntervention(string workshopModId, List<string> selectedPbos, List<string> selectedExtensions);
    List<DomainWorkshopMod> GetPendingReleaseMods();
}

public class WorkshopModsService(
    IWorkshopModsContext workshopModsContext,
    ISteamApiService steamApiService,
    IPublishEndpoint publishEndpoint,
    IUksfLogger logger
) : IWorkshopModsService
{
    public List<DomainWorkshopMod> GetPendingReleaseMods()
    {
        return workshopModsContext.Get()
                                  .Where(m => m.Status is WorkshopModStatus.InstalledPendingRelease or WorkshopModStatus.UpdatedPendingRelease
                                             or WorkshopModStatus.UninstalledPendingRelease
                                  )
                                  .ToList();
    }

    public async Task<Dictionary<string, DateTime>> GetWorkshopModUpdatedDates()
    {
        var steamIds = workshopModsContext.Get().Select(x => x.SteamId).Distinct().ToList();
        var infos = await steamApiService.GetWorkshopModInfos(steamIds);
        return infos.ToDictionary(x => x.Key, x => x.Value.UpdatedDate);
    }

    public async Task InstallWorkshopMod(string workshopModId, bool rootMod, string folderName = null)
    {
        var existingMod = workshopModsContext.GetSingle(x => x.SteamId == workshopModId);
        if (existingMod is not null && existingMod.Status != WorkshopModStatus.Uninstalled)
        {
            throw new BadRequestException($"Workshop mod with Steam ID {workshopModId} already exists");
        }

        var modInfo = await steamApiService.GetWorkshopModInfo(workshopModId);
        DomainWorkshopMod workshopMod;
        if (existingMod is null)
        {
            workshopMod = new DomainWorkshopMod
            {
                SteamId = workshopModId,
                Name = modInfo.Name,
                Status = WorkshopModStatus.Installing,
                RootMod = rootMod,
                FolderName = folderName,
                LastOperation = WorkshopModOperationType.Install
            };
            await workshopModsContext.Add(workshopMod);
        }
        else
        {
            existingMod.Name = modInfo.Name;
            existingMod.Status = WorkshopModStatus.Installing;
            existingMod.RootMod = rootMod;
            existingMod.FolderName = folderName;
            existingMod.Pbos = [];
            existingMod.Extensions = [];
            existingMod.AvailablePbos = [];
            existingMod.AvailableExtensions = [];
            existingMod.StatusMessage = null;
            existingMod.ErrorMessage = null;
            existingMod.LastOperation = WorkshopModOperationType.Install;
            await workshopModsContext.Replace(existingMod);
            workshopMod = existingMod;
        }

        logger.LogAudit($"Workshop mod installed: {workshopModId}, {workshopMod.Name}");

        await publishEndpoint.Publish(new WorkshopModInstallCommand { WorkshopModId = workshopModId });
    }

    public async Task UpdateWorkshopMod(string workshopModId)
    {
        var workshopMod = workshopModsContext.GetSingle(x => x.SteamId == workshopModId);
        if (workshopMod == null)
        {
            throw new NotFoundException($"Cannot find workshop mod with Steam ID {workshopModId}");
        }

        if (workshopMod.Status == WorkshopModStatus.Updating)
        {
            throw new BadRequestException($"Workshop mod is already being updated: {workshopMod.Name}");
        }

        if (workshopMod.Status == WorkshopModStatus.InterventionRequired)
        {
            throw new BadRequestException($"Workshop mod requires manual intervention: {workshopMod.Name}");
        }

        var modInfo = await steamApiService.GetWorkshopModInfo(workshopModId);
        if (modInfo.UpdatedDate <= workshopMod.LastUpdatedLocally)
        {
            throw new BadRequestException($"No update available for {workshopMod.Name}");
        }

        workshopMod.Status = WorkshopModStatus.Updating;
        workshopMod.StatusMessage = "Preparing to update...";
        workshopMod.LastOperation = WorkshopModOperationType.Update;
        await workshopModsContext.Replace(workshopMod);
        logger.LogAudit($"Workshop mod updated: {workshopModId}, {workshopMod.Name}");

        await publishEndpoint.Publish(new WorkshopModUpdateCommand { WorkshopModId = workshopModId });
    }

    public async Task RetryWorkshopMod(string workshopModId)
    {
        var workshopMod = workshopModsContext.GetSingle(x => x.SteamId == workshopModId);
        if (workshopMod == null)
        {
            throw new NotFoundException($"Cannot find workshop mod with Steam ID {workshopModId}");
        }

        if (workshopMod.Status != WorkshopModStatus.Error)
        {
            throw new BadRequestException($"Workshop mod is not in an error state: {workshopMod.Name}");
        }

        // Mods that errored before operation tracking existed have no recorded operation; they are installed mods whose
        // update failed, so reprocessing them as an update is the safe default.
        var operation = workshopMod.LastOperation ?? WorkshopModOperationType.Update;

        workshopMod.ErrorMessage = null;
        switch (operation)
        {
            case WorkshopModOperationType.Install:
                workshopMod.Status = WorkshopModStatus.Installing;
                workshopMod.StatusMessage = "Retrying install...";
                await workshopModsContext.Replace(workshopMod);
                await publishEndpoint.Publish(new WorkshopModInstallCommand { WorkshopModId = workshopModId });
                break;
            case WorkshopModOperationType.Uninstall:
                workshopMod.Status = WorkshopModStatus.Uninstalling;
                workshopMod.StatusMessage = "Retrying uninstall...";
                await workshopModsContext.Replace(workshopMod);
                await publishEndpoint.Publish(new WorkshopModUninstallCommand { WorkshopModId = workshopModId });
                break;
            default:
                workshopMod.Status = WorkshopModStatus.Updating;
                workshopMod.StatusMessage = "Retrying update...";
                await workshopModsContext.Replace(workshopMod);
                await publishEndpoint.Publish(new WorkshopModUpdateCommand { WorkshopModId = workshopModId });
                break;
        }

        logger.LogAudit($"Workshop mod retry ({operation}): {workshopModId}, {workshopMod.Name}");
    }

    public async Task UninstallWorkshopMod(string workshopModId)
    {
        var workshopMod = workshopModsContext.GetSingle(x => x.SteamId == workshopModId);
        if (workshopMod == null)
        {
            throw new NotFoundException($"Cannot find workshop mod with Steam ID {workshopModId}");
        }

        if (workshopMod.Status == WorkshopModStatus.Uninstalled)
        {
            throw new BadRequestException($"Workshop mod is already uninstalled: {workshopMod.Name}");
        }

        var otherMods = workshopModsContext.Get().Where(x => x.SteamId != workshopModId && x.Status != WorkshopModStatus.Uninstalled).ToList();
        var otherModFiles = otherMods.SelectMany(x => x.Pbos).Concat(otherMods.SelectMany(x => x.Extensions ?? []));
        var modFiles = workshopMod.Pbos.Concat(workshopMod.Extensions ?? []);
        var conflicts = otherModFiles.Intersect(modFiles, StringComparer.OrdinalIgnoreCase).ToList();
        if (conflicts.Count != 0)
        {
            throw new BadRequestException(
                $"Cannot uninstall mod '{workshopMod.Name}' because other mods depend on these files: {string.Join(", ", conflicts)}"
            );
        }

        workshopMod.Status = WorkshopModStatus.Uninstalling;
        workshopMod.StatusMessage = "Preparing to uninstall...";
        workshopMod.LastOperation = WorkshopModOperationType.Uninstall;
        await workshopModsContext.Replace(workshopMod);
        logger.LogAudit($"Workshop mod uninstalled: {workshopModId}, {workshopMod.Name}");

        await publishEndpoint.Publish(new WorkshopModUninstallCommand { WorkshopModId = workshopModId });
    }

    public async Task ResolveWorkshopModManualIntervention(string workshopModId, List<string> selectedPbos, List<string> selectedExtensions)
    {
        var workshopMod = workshopModsContext.GetSingle(x => x.SteamId == workshopModId);
        if (workshopMod == null)
        {
            throw new NotFoundException($"Cannot find workshop mod with Steam ID {workshopModId}");
        }

        if (workshopMod.Status != WorkshopModStatus.InterventionRequired)
        {
            throw new BadRequestException($"Workshop mod does not require manual intervention: {workshopMod.Name}");
        }

        if ((selectedPbos == null || selectedPbos.Count == 0) && (selectedExtensions == null || selectedExtensions.Count == 0))
        {
            throw new BadRequestException($"Nothing selected to install for workshop mod with Steam ID {workshopModId}");
        }

        await publishEndpoint.Publish(
            new WorkshopModInterventionResolved
            {
                WorkshopModId = workshopModId,
                SelectedPbos = selectedPbos ?? [],
                SelectedExtensions = selectedExtensions ?? []
            }
        );
    }

    public async Task DeleteWorkshopMod(string workshopModId)
    {
        var workshopMod = workshopModsContext.GetSingle(x => x.SteamId == workshopModId);
        if (workshopMod == null)
        {
            throw new NotFoundException($"Cannot find workshop mod with Steam ID {workshopModId}");
        }

        if (workshopMod.Status != WorkshopModStatus.Uninstalled)
        {
            throw new BadRequestException($"Workshop mod must be uninstalled first: {workshopMod.Name}");
        }

        logger.LogAudit($"Workshop mod deleted: {workshopModId}, {workshopMod.Name}");
        await workshopModsContext.Delete(workshopMod);
    }
}
