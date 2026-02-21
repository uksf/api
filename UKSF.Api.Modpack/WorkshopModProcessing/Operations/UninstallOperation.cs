using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;

namespace UKSF.Api.Modpack.WorkshopModProcessing.Operations;

public sealed class UninstallOperation(IWorkshopModsContext workshopModsContext, IWorkshopModsProcessingService workshopModsProcessingService)
    : WorkshopModOperationBase(workshopModsContext, workshopModsProcessingService), IUninstallOperation
{
    private WorkshopModStatus _previousStatus;

    protected override WorkshopModStatus ActiveStatus => WorkshopModStatus.Uninstalling;
    protected override string CancelPrefix => "Uninstall";
    protected override WorkshopModStatus CompletedStatus => WorkshopModStatus.Uninstalled;
    protected override string CompletedMessage => "Uninstalled";
    protected override string ActiveStatusMessage => "Uninstalling...";

    protected override void OnBeforeExecute(DomainWorkshopMod workshopMod)
    {
        _previousStatus = workshopMod.Status;
    }

    protected override OperationResult ShouldSkipExecution(DomainWorkshopMod workshopMod)
    {
        if (workshopMod.Status is WorkshopModStatus.Uninstalled or WorkshopModStatus.UninstalledPendingRelease)
        {
            return OperationResult.Successful(filesChanged: false);
        }

        return null;
    }

    protected override Task ExecuteCoreAsync(DomainWorkshopMod workshopMod, List<string> selectedPbos, CancellationToken cancellationToken)
    {
        ExecutionFilesChanged = false;

        if (workshopMod.RootMod)
        {
            WorkshopModsProcessingService.DeleteRootModFromRepos(workshopMod);
            ExecutionFilesChanged = true;
        }
        else
        {
            var pbosToDelete = workshopMod.Pbos ?? [];
            if (pbosToDelete.Count > 0)
            {
                WorkshopModsProcessingService.DeletePbosFromDependencies(pbosToDelete);
                ExecutionFilesChanged = true;
            }
        }

        return Task.CompletedTask;
    }

    protected override void ApplyCompletedState(DomainWorkshopMod workshopMod)
    {
        base.ApplyCompletedState(workshopMod);

        if (_previousStatus is WorkshopModStatus.Installed or WorkshopModStatus.InstalledPendingRelease or WorkshopModStatus.UpdatedPendingRelease)
        {
            workshopMod.Status = WorkshopModStatus.UninstalledPendingRelease;
            workshopMod.StatusMessage = "Uninstalled pending next modpack release";
        }

        workshopMod.Pbos = [];
    }
}
