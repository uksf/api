using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.BuildProcess.Steps.ReleaseSteps;

[BuildStep(Name)]
public class BuildStepProcessWorkshopMods : BuildStep
{
    private IWorkshopModsContext _workshopModsContext;
    public const string Name = "Workshop Mods";

    protected override Task SetupExecute()
    {
        _workshopModsContext = ServiceProvider.GetService<IWorkshopModsContext>();
        StepLogger.Log("Retrieved services");
        return Task.CompletedTask;
    }

    protected override async Task ProcessExecute()
    {
        StepLogger.LogSurround("\nProcessing workshop mods pending installation...");
        var workshopModsPendingInstall = _workshopModsContext.Get().Where(x => x.Status == WorkshopModStatus.InstalledPendingRelease).ToList();
        foreach (var workshopMod in workshopModsPendingInstall)
        {
            workshopMod.ModpackVersionFirstAdded = Build.Version;
            workshopMod.Status = WorkshopModStatus.Installed;
            workshopMod.StatusMessage = "Installed";
            await _workshopModsContext.Replace(workshopMod);
            StepLogger.Log($"Processed installation for workshop mod: {workshopMod.Name}");
        }

        StepLogger.LogSurround("Processed workshop mods pending installation");

        StepLogger.LogSurround("\nProcessing workshop mods pending update...");
        var workshopModsPendingUpdate = _workshopModsContext.Get().Where(x => x.Status == WorkshopModStatus.UpdatedPendingRelease).ToList();
        foreach (var workshopMod in workshopModsPendingUpdate)
        {
            workshopMod.ModpackVersionLastUpdated = Build.Version;
            workshopMod.Status = WorkshopModStatus.Installed;
            workshopMod.StatusMessage = "Installed";
            await _workshopModsContext.Replace(workshopMod);
            StepLogger.Log($"Processed update for workshop mod: {workshopMod.Name}");
        }

        StepLogger.LogSurround("Processed workshop mods pending update");

        StepLogger.LogSurround("\nProcessing workshop mods pending uninstallation...");
        var workshopModsPendingUninstall = _workshopModsContext.Get().Where(x => x.Status == WorkshopModStatus.UninstalledPendingRelease).ToList();
        foreach (var workshopMod in workshopModsPendingUninstall)
        {
            workshopMod.ModpackVersionFirstAdded = null;
            workshopMod.ModpackVersionLastUpdated = null;
            workshopMod.Status = WorkshopModStatus.Uninstalled;
            workshopMod.StatusMessage = "Uninstalled";
            await _workshopModsContext.Replace(workshopMod);
            StepLogger.Log($"Processed uninstallation for workshop mod: {workshopMod.Name}");
        }

        StepLogger.LogSurround("Processed workshop mods pending uninstallation");

        StepLogger.Log("Workshop mods processing completed");
    }
}
