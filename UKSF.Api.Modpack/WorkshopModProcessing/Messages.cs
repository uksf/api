namespace UKSF.Api.Modpack.WorkshopModProcessing;

public interface IWorkshopModCommand
{
    string WorkshopModId { get; }
}

// Initial Commands (Entry points)
public class WorkshopModInstallCommand : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
}

public class WorkshopModUpdateCommand : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
}

public class WorkshopModUninstallCommand : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
}

public class WorkshopModInterventionResolved : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
    public List<string> SelectedPbos { get; init; }
}

// Internal Commands
public class WorkshopModInstallDownloadCommand : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
}

public class WorkshopModUpdateDownloadCommand : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
}

public class WorkshopModInstallCheckCommand : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
}

public class WorkshopModUpdateCheckCommand : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
}

public class WorkshopModInstallInternalCommand : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
    public List<string> SelectedPbos { get; init; }
}

public class WorkshopModUpdateInternalCommand : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
    public List<string> SelectedPbos { get; init; }
}

public class WorkshopModUninstallInternalCommand : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
}

public class WorkshopModCleanupCommand : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
    public bool FilesChanged { get; init; }
}

// Events (Completions)
public class WorkshopModInstallDownloadComplete : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
}

public class WorkshopModUpdateDownloadComplete : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
}

public class WorkshopModInstallCheckComplete : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
    public bool InterventionRequired { get; init; }
}

public class WorkshopModUpdateCheckComplete : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
    public bool InterventionRequired { get; init; }
}

public class WorkshopModInstallComplete : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
    public bool FilesChanged { get; init; }
}

public class WorkshopModUpdateComplete : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
    public bool FilesChanged { get; init; }
}

public class WorkshopModUninstallComplete : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
    public bool FilesChanged { get; init; }
}

public class WorkshopModCleanupComplete : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
}

// Fault Events
public class WorkshopModOperationFaulted : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
    public string ErrorMessage { get; init; }
    public string FaultedState { get; init; }
    public DateTime FaultedAt { get; init; } = DateTime.UtcNow;
}
