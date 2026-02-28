using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Operations;

public sealed class UpdateOperation(IWorkshopModsContext workshopModsContext, IWorkshopModsProcessingService workshopModsProcessingService)
    : WorkshopModOperationBase(workshopModsContext, workshopModsProcessingService), IUpdateOperation
{
    protected override WorkshopModStatus ActiveStatus => WorkshopModStatus.Updating;
    protected override string CancelPrefix => "Update";
    protected override WorkshopModStatus CompletedStatus => WorkshopModStatus.UpdatedPendingRelease;
    protected override string CompletedMessage => "Updated pending next modpack release";
    protected override string ActiveStatusMessage => "Updating...";

    protected override async Task ExecuteCoreAsync(DomainWorkshopMod workshopMod, List<string> selectedPbos, CancellationToken cancellationToken)
    {
        if (workshopMod.RootMod)
        {
            ExecutionFilesChanged = WorkshopModsProcessingService.SyncRootModToRepos(workshopMod);
        }
        else
        {
            await WorkshopModsProcessingService.CopyPbosToDependencies(workshopMod, selectedPbos, cancellationToken);

            var oldPbos = workshopMod.Pbos ?? [];
            var pbosToDelete = oldPbos.Except(selectedPbos, StringComparer.OrdinalIgnoreCase).ToList();
            if (pbosToDelete.Count > 0)
            {
                WorkshopModsProcessingService.DeletePbosFromDependencies(pbosToDelete);
            }

            workshopMod.Pbos = selectedPbos;
        }
    }
}
