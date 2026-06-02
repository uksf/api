using UKSF.Api.Core.Models;
using UKSF.Api.Modpack.WorkshopModProcessing;

namespace UKSF.Api.Modpack.Models;

public enum WorkshopModStatus
{
    Installing,
    InstalledPendingRelease,
    Installed,
    Updating,
    UpdatedPendingRelease,
    Uninstalling,
    Uninstalled,
    UninstalledPendingRelease,
    Error,
    InterventionRequired
}

public class DomainWorkshopMod : MongoObject
{
    public string SteamId { get; set; }
    public string Name { get; set; }
    public bool RootMod { get; set; }
    public string FolderName { get; set; }
    public List<string> Pbos { get; set; } = [];
    public List<string> AvailablePbos { get; set; } = [];
    public DateTime LastUpdatedLocally { get; set; }
    public string ModpackVersionFirstAdded { get; set; }
    public string ModpackVersionLastUpdated { get; set; }
    public List<string> CustomFilesList { get; set; } = [];
    public WorkshopModStatus Status { get; set; }
    public string StatusMessage { get; set; }
    public string ErrorMessage { get; set; }

    /// <summary>The last operation requested for this mod, used to retry it after it ends up in the Error state.</summary>
    public WorkshopModOperationType? LastOperation { get; set; }
}
