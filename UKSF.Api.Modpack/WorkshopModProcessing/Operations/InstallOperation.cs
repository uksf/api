using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Operations;

public sealed class InstallOperation(
    IWorkshopModsContext workshopModsContext,
    IWorkshopModsProcessingService workshopModsProcessingService,
    IWorkshopModDependencyFilesService workshopModDependencyFilesService,
    IWorkshopModRootFilesService workshopModRootFilesService
) : WorkshopModOperationBase(workshopModsContext, workshopModsProcessingService, workshopModDependencyFilesService, workshopModRootFilesService),
    IInstallOperation
{
    protected override WorkshopModStatus ActiveStatus => WorkshopModStatus.Installing;
    protected override string CancelPrefix => "Install";
    protected override WorkshopModStatus CompletedStatus => WorkshopModStatus.InstalledPendingRelease;
    protected override string CompletedMessage => "Installed pending next modpack release";
    protected override string ActiveStatusMessage => "Installing...";

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

        workshopMod.Pbos = selectedPbos;
        workshopMod.Extensions = selectedExtensions;
        workshopMod.AvailablePbos = [];
        workshopMod.AvailableExtensions = [];

        return Task.CompletedTask;
    }
}
