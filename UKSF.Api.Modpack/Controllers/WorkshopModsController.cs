using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Models.Request;
using UKSF.Api.Modpack.Models.Response;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.Controllers;

[Route("workshop")]
public class WorkshopModsController(IWorkshopModsService workshopModsService, IWorkshopModsContext workshopModsContext) : ControllerBase
{
    [HttpGet]
    [Permissions(Permissions.Member)]
    public List<WorkshopModResponse> GetWorkshopMods()
    {
        return workshopModsContext.Get().Select(MapToResponse).ToList();
    }

    [HttpGet("{id}")]
    [Permissions(Permissions.Member)]
    public WorkshopModResponse GetWorkshopModById([FromRoute] string id)
    {
        var workshopMod = workshopModsContext.GetSingle(x => x.Id == id);
        if (workshopMod == null)
        {
            throw new NotFoundException($"Workshop mod with ID {id} not found");
        }

        return MapToResponse(workshopMod);
    }

    [HttpGet("updatedDates")]
    [Permissions(Permissions.Member)]
    public async Task<List<WorkshopModUpdatedDateResponse>> GetWorkshopModUpdatedDates()
    {
        var updatedDates = await workshopModsService.GetWorkshopModUpdatedDates();
        return updatedDates.Select(x => new WorkshopModUpdatedDateResponse { SteamId = x.Key, UpdatedDate = x.Value.ToString("o") }).ToList();
    }

    [HttpPost]
    [Permissions(Permissions.Admin)]
    public Task InstallWorkshopMod([FromBody] InstallWorkshopModRequest request)
    {
        return workshopModsService.InstallWorkshopMod(request.SteamId, request.RootMod, request.FolderName);
    }

    [HttpPost("{workshopModId}/update")]
    [Permissions(Permissions.Admin)]
    public Task UpdateWorkshopMod([FromRoute] string workshopModId)
    {
        return workshopModsService.UpdateWorkshopMod(workshopModId);
    }

    [HttpPost("{workshopModId}/retry")]
    [Permissions(Permissions.Admin)]
    public Task RetryWorkshopMod([FromRoute] string workshopModId)
    {
        return workshopModsService.RetryWorkshopMod(workshopModId);
    }

    [HttpPost("{workshopModId}/uninstall")]
    [Permissions(Permissions.Admin)]
    public Task UninstallWorkshopMod([FromRoute] string workshopModId)
    {
        return workshopModsService.UninstallWorkshopMod(workshopModId);
    }

    [HttpPost("{workshopModId}/resolve")]
    [Permissions(Permissions.Admin)]
    public Task ResolveWorkshopModManualIntervention([FromRoute] string workshopModId, [FromBody] WorkshopModResolveInterventionRequest request)
    {
        return workshopModsService.ResolveWorkshopModManualIntervention(workshopModId, request.SelectedPbos, request.SelectedExtensions);
    }

    [HttpDelete("{workshopModId}")]
    [Permissions(Permissions.Admin)]
    public Task DeleteWorkshopMod([FromRoute] string workshopModId)
    {
        return workshopModsService.DeleteWorkshopMod(workshopModId);
    }

    private static WorkshopModResponse MapToResponse(DomainWorkshopMod mod)
    {
        return new WorkshopModResponse
        {
            Id = mod.Id,
            SteamId = mod.SteamId,
            Name = mod.Name,
            RootMod = mod.RootMod,
            FolderName = mod.FolderName,
            Status = mod.Status.ToString(),
            StatusMessage = mod.StatusMessage,
            ErrorMessage = mod.ErrorMessage,
            LastUpdatedLocally = mod.LastUpdatedLocally.ToString("o"),
            ModpackVersionFirstAdded = mod.ModpackVersionFirstAdded,
            ModpackVersionLastUpdated = mod.ModpackVersionLastUpdated,
            Pbos = mod.Pbos,
            Extensions = mod.Extensions,
            AvailablePbos = mod.AvailablePbos,
            AvailablePboFolders = mod.AvailablePboFolders ?? [],
            AvailableExtensions = mod.AvailableExtensions,
            LockedPbos = mod.LockedPbos ?? [],
            LockedExtensions = mod.LockedExtensions ?? []
        };
    }
}
