using Microsoft.AspNetCore.Mvc;
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
    public List<WorkshopModResponse> GetWorkshopMods()
    {
        return workshopModsContext.Get().Select(MapToResponse).ToList();
    }

    [HttpGet("{workshopModId}")]
    public WorkshopModResponse GetWorkshopMod([FromRoute] string workshopModId)
    {
        var workshopMod = workshopModsContext.GetSingle(workshopModId);
        if (workshopMod == null)
        {
            throw new NotFoundException("Workshop mod with ID {workshopModId} not found");
        }

        return MapToResponse(workshopMod);
    }

    [HttpGet("{workshopModId}/updatedDate")]
    public async Task<WorkshopModUpdatedDateResponse> GetWorkshopModUpdatedDate([FromRoute] string workshopModId)
    {
        var updatedDate = await workshopModsService.GetWorkshopModUpdatedDate(workshopModId);
        return new WorkshopModUpdatedDateResponse { UpdatedDate = updatedDate.ToString("o") };
    }

    [HttpPost]
    public Task InstallWorkshopMod([FromBody] InstallWorkshopModRequest request)
    {
        return workshopModsService.InstallWorkshopMod(request.SteamId, request.RootMod);
    }

    [HttpPost("{workshopModId}/update")]
    public Task UpdateWorkshopMod([FromRoute] string workshopModId)
    {
        return workshopModsService.UpdateWorkshopMod(workshopModId);
    }

    [HttpPost("{workshopModId}/uninstall")]
    public Task UninstallWorkshopMod([FromRoute] string workshopModId)
    {
        return workshopModsService.UninstallWorkshopMod(workshopModId);
    }

    [HttpPost("{workshopModId}/resolve")]
    public Task ResolveWorkshopModManualIntervention([FromRoute] string workshopModId, [FromBody] WorkshopModResolveInterventionRequest request)
    {
        return workshopModsService.ResolveWorkshopModManualIntervention(workshopModId, request.SelectedPbos);
    }

    [HttpDelete("{workshopModId}")]
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
            Status = mod.Status,
            StatusMessage = mod.StatusMessage,
            ErrorMessage = mod.ErrorMessage,
            LastUpdatedLocally = mod.LastUpdatedLocally.ToString("o"),
            ModpackVersionFirstAdded = mod.ModpackVersionFirstAdded,
            ModpackVersionLastUpdated = mod.ModpackVersionLastUpdated,
            Pbos = mod.Pbos,
            CustomFilesList = mod.CustomFilesList
        };
    }
}

/*
 * 2 parts
 * 1. download workshop mods to use on the server ad-hoc
 * 2. set a workshop mod to be part of the modpack
 *
 * 1.
 * - on website, enter a workshop mod id
 * - mod is downloaded to the server
 * - mod can be selected to be used with a server
 * - see list of mods that are downloaded on the server
 * - mods show if there is an update available. button to update the mod. not automatic
 * - maybe updates should be automatic for ad-hoc mods?
 * - maybe each mod has a setting to allow it to be updated automatically?
 * - mod should be deletable from the list. all files removed
 * - mods should be downloaded and moved to reduce disk usage
 * - how to handle keys for mods that are downloaded? always move to Release keys folder? only move on launch?
 * - deleting a mod should delete its key from Release keys
 *
 * 2.
 * - on website, view a list of workshop mods that are in the modpack
 * - is it the same list as ad-hoc mods? no. share components
 * - workshop id can be entered to download mod
 * - when a mod is added to the modpack, it is always added to Dev and RC. mod is then automatically part of next release
 * - modpack workshop mods need moving to dependencies to reduce number of modpack parts
 * - each mod needs to track its list of pbos
 * - key management not required as part of modpack build process
 * - should the mod list differentiate between mods that have been released and those that haven't? does the list show which version it was released with?
 * - mods should be deletable from the list. when a mod is deleted, does it show in the list until the next release?
 * - the mod list should show when an update is available. mods that are updated are updated in the next release by being moved to RC.
 * - deleting a mod should check for the same pbos in other mods that if deleted would cause issues
 * - what if the mod has more than just pbos? like dlls? don't handle, throw a warning when adding mod that there are custom files to handle. let admin handle.
 * - what if we want the mod to be at the modpack root? for example CBA and CUP. handle later
 *
 * 2 is more useful than 1. 1 is easier to build. build 2 first.
 *
 * Reeqiurements
 * endpoints needed
 * - workshop mod list
 * - add mod to list
 * - delete mod
 * - update mod
 * - get last updated date from workshop for mod
 *
 * functionality needed
 * - download mod to server
 * - move mod pbo files to Dev and RC dependencies folder
 * - update mod. needs to handle when pbos change names
 * - delete mod pbos from Dev and RC
 *
 * workshop mod db model
 * - workshop id
 * - mod name (for display only)
 * - list of pbos
 * - last updated date
 * - marked for deletion
 * - release ID the mod was released with
 */
