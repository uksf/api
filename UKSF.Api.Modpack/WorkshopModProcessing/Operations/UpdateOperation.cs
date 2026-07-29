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

    protected override Task ExecuteCoreAsync(DomainWorkshopMod workshopMod, List<string> selectedPbos, CancellationToken cancellationToken)
    {
        if (workshopMod.RootMod)
        {
            ExecutionFilesChanged = WorkshopModRootFilesService.SyncRootModToRepos(workshopMod);
            return Task.CompletedTask;
        }

        var extensionFiles = WorkshopModsProcessingService.GetExtensionFiles(WorkshopModsProcessingService.GetWorkshopModPath(workshopMod.SteamId));

        WorkshopModDependencyFilesService.CopyPbosToDependencies(workshopMod, selectedPbos, cancellationToken);
        WorkshopModDependencyFilesService.CopyExtensionFilesToDependencies(workshopMod, extensionFiles, cancellationToken);

        var pbosToDelete = (workshopMod.Pbos ?? []).Except(selectedPbos, StringComparer.OrdinalIgnoreCase).ToList();
        if (pbosToDelete.Count > 0)
        {
            WorkshopModDependencyFilesService.DeletePbosFromDependencies(pbosToDelete);
        }

        var extensionFilesToDelete = (workshopMod.ExtensionFiles ?? []).Except(extensionFiles, StringComparer.OrdinalIgnoreCase).ToList();
        if (extensionFilesToDelete.Count > 0)
        {
            WorkshopModDependencyFilesService.DeleteExtensionFilesFromDependencies(extensionFilesToDelete);
        }

        workshopMod.Pbos = selectedPbos;
        workshopMod.ExtensionFiles = extensionFiles;
        workshopMod.AvailablePbos = [];

        return Task.CompletedTask;
    }
}
