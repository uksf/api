using MassTransit;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.WorkshopModProcessing;

namespace UKSF.Api.Modpack.Services;

public interface IWorkshopModsService
{
    Task<DateTime> GetWorkshopModUpdatedDate(string workshopModId);
    Task InstallWorkshopMod(string workshopModId, bool rootMod);
    Task UpdateWorkshopMod(string workshopModId);
    Task UninstallWorkshopMod(string workshopModId);
    Task DeleteWorkshopMod(string workshopModId);
    Task ResolveWorkshopModManualIntervention(string workshopModId, List<string> selectedPbos);
}

public class WorkshopModsService(
    IWorkshopModsContext workshopModsContext,
    ISteamApiService steamApiService,
    IPublishEndpoint publishEndpoint,
    IUksfLogger logger
) : IWorkshopModsService
{
    public async Task<DateTime> GetWorkshopModUpdatedDate(string workshopModId)
    {
        var info = await steamApiService.GetWorkshopModInfo(workshopModId);
        return info.UpdatedDate;
    }

    public async Task InstallWorkshopMod(string workshopModId, bool rootMod)
    {
        var existingMod = workshopModsContext.Get().FirstOrDefault(x => x.SteamId == workshopModId && x.Status != WorkshopModStatus.Uninstalled);
        if (existingMod != null)
        {
            throw new BadRequestException($"Workshop mod with ID {workshopModId} already exists");
        }

        var modInfo = await steamApiService.GetWorkshopModInfo(workshopModId);
        var workshopMod = new DomainWorkshopMod
        {
            SteamId = workshopModId,
            Name = modInfo.Name,
            Status = WorkshopModStatus.Installing,
            RootMod = rootMod
        };
        await workshopModsContext.Add(workshopMod);
        logger.LogAudit($"Workshop mod installed: {workshopModId}, {workshopMod.Name}");

        await publishEndpoint.Publish(new WorkshopModInstallCommand { WorkshopModId = workshopMod.Id });
    }

    public async Task UpdateWorkshopMod(string workshopModId)
    {
        var workshopMod = workshopModsContext.GetSingle(workshopModId);
        if (workshopMod == null)
        {
            throw new NotFoundException($"Cannot find workshop mod with ID {workshopModId}");
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
        await workshopModsContext.Replace(workshopMod);
        logger.LogAudit($"Workshop mod updated: {workshopModId}, {workshopMod.Name}");

        await publishEndpoint.Publish(new WorkshopModUpdateCommand { WorkshopModId = workshopModId });
    }

    public async Task UninstallWorkshopMod(string workshopModId)
    {
        var workshopMod = workshopModsContext.GetSingle(workshopModId);
        if (workshopMod == null)
        {
            throw new NotFoundException($"Cannot find workshop mod with ID {workshopModId}");
        }

        if (workshopMod.Status == WorkshopModStatus.Uninstalled)
        {
            throw new BadRequestException($"Workshop mod is already uninstalled: {workshopMod.Name}");
        }

        var otherModPbos = workshopModsContext.Get()
                                              .Where(x => x.Id != workshopModId && x.Status != WorkshopModStatus.Uninstalled)
                                              .SelectMany(x => x.Pbos)
                                              .ToList();
        var conflicts = otherModPbos.Intersect(workshopMod.Pbos, StringComparer.OrdinalIgnoreCase).ToList();
        if (conflicts.Count != 0)
        {
            throw new BadRequestException($"Cannot uninstall mod '{workshopMod.Name}' because other mods depend on these PBOs: {string.Join(", ", conflicts)}");
        }

        workshopMod.Status = WorkshopModStatus.Uninstalling;
        workshopMod.StatusMessage = "Preparing to uninstall...";
        await workshopModsContext.Replace(workshopMod);
        logger.LogAudit($"Workshop mod uninstalled: {workshopModId}, {workshopMod.Name}");

        await publishEndpoint.Publish(new WorkshopModUninstallCommand { WorkshopModId = workshopModId });
    }

    public async Task ResolveWorkshopModManualIntervention(string workshopModId, List<string> selectedPbos)
    {
        var workshopMod = workshopModsContext.GetSingle(workshopModId);
        if (workshopMod == null)
        {
            throw new NotFoundException($"Cannot find workshop mod with ID {workshopModId}");
        }

        if (workshopMod.Status != WorkshopModStatus.InterventionRequired)
        {
            throw new BadRequestException($"Workshop mod does not require manual intervention: {workshopMod.Name}");
        }

        await publishEndpoint.Publish(new WorkshopModInterventionResolved { WorkshopModId = workshopModId, SelectedPbos = selectedPbos ?? [] });
    }

    public async Task DeleteWorkshopMod(string workshopModId)
    {
        var workshopMod = workshopModsContext.GetSingle(workshopModId);
        if (workshopMod == null)
        {
            throw new NotFoundException($"Cannot find workshop mod with ID {workshopModId}");
        }

        if (workshopMod.Status != WorkshopModStatus.Uninstalled)
        {
            throw new BadRequestException($"Workshop mod must be uninstalled first: {workshopMod.Name}");
        }

        logger.LogAudit($"Workshop mod deleted: {workshopModId}, {workshopMod.Name}");
        await workshopModsContext.Delete(workshopMod);
    }
}
