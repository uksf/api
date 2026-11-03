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

    public List<string> Extensions { get; set; } = [];

    public List<string> AvailablePbos { get; set; } = [];

    public List<WorkshopModPbo> AvailablePboFolders { get; set; } = [];

    public List<string> AvailableExtensions { get; set; } = [];
    public List<string> LockedPbos { get; set; } = [];
    public List<string> LockedExtensions { get; set; } = [];
    public DateTime LastUpdatedLocally { get; set; }
    public string ModpackVersionFirstAdded { get; set; }
    public string ModpackVersionLastUpdated { get; set; }

    public WorkshopModStatus Status { get; set; }
    public string StatusMessage { get; set; }
    public string ErrorMessage { get; set; }

    public WorkshopModOperationType? LastOperation { get; set; }
}
