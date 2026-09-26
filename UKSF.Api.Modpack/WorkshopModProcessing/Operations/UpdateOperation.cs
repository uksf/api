using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Operations;

public sealed class UpdateOperation(
    IWorkshopModsContext workshopModsContext,
    IWorkshopModsProcessingService workshopModsProcessingService,
    IWorkshopModDependencyFilesService workshopModDependencyFilesService,
    IWorkshopModRootFilesService workshopModRootFilesService
) : WorkshopModOperationBase(workshopModsContext, workshopModsProcessingService, workshopModDependencyFilesService, workshopModRootFilesService),
    IUpdateOperation
{
    protected override WorkshopModStatus ActiveStatus => WorkshopModStatus.Updating;
    protected override string CancelPrefix => "Update";
    protected override WorkshopModStatus CompletedStatus => WorkshopModStatus.UpdatedPendingRelease;
    protected override string CompletedMessage => "Updated pending next modpack release";
    protected override string ActiveStatusMessage => "Updating...";

    protected override Task ExecuteCoreAsync(
        DomainWorkshopMod workshopMod,
        List<string> selectedPbos,
        List<string> selectedExtensions,
        CancellationToken cancellationToken
    )
    {
        if (workshopMod.RootMod)
        {
            ExecutionFilesChanged = WorkshopModRootFilesService.SyncRootModToRepos(workshopMod);
            return Task.CompletedTask;
        }

        WorkshopModDependencyFilesService.CopyPbosToDependencies(workshopMod, selectedPbos, cancellationToken);
        WorkshopModDependencyFilesService.CopyExtensionsToDependencies(workshopMod, selectedExtensions, cancellationToken);

        DeleteUnselectedFiles(workshopMod, selectedPbos, selectedExtensions);

        workshopMod.Pbos = selectedPbos;
        workshopMod.Extensions = selectedExtensions;
        workshopMod.AvailablePbos = [];
        workshopMod.AvailablePboFolders = [];
        workshopMod.AvailableExtensions = [];

        return Task.CompletedTask;
    }
}
