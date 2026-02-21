using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Operations;

public sealed class InstallOperation(IWorkshopModsContext workshopModsContext, IWorkshopModsProcessingService workshopModsProcessingService)
    : WorkshopModOperationBase(workshopModsContext, workshopModsProcessingService), IInstallOperation
{
    protected override WorkshopModStatus ActiveStatus => WorkshopModStatus.Installing;
    protected override string CancelPrefix => "Install";
    protected override WorkshopModStatus CompletedStatus => WorkshopModStatus.InstalledPendingRelease;
    protected override string CompletedMessage => "Installed pending next modpack release";
    protected override string ActiveStatusMessage => "Installing...";

    protected override async Task ExecuteCoreAsync(DomainWorkshopMod workshopMod, List<string> selectedPbos, CancellationToken cancellationToken)
    {
        if (workshopMod.RootMod)
        {
            await WorkshopModsProcessingService.CopyRootModToRepos(workshopMod, cancellationToken);
        }
        else
        {
            await WorkshopModsProcessingService.CopyPbosToDependencies(workshopMod, selectedPbos, cancellationToken);
            workshopMod.Pbos = selectedPbos;
        }
    }
}
