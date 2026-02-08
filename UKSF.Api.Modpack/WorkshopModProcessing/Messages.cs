namespace UKSF.Api.Modpack.WorkshopModProcessing;

public enum WorkshopModOperationType
{
    Install,
    Update
}

public interface IWorkshopModCommand
{
    string WorkshopModId { get; }
}

// Initial Commands (Entry points - kept separate for distinct API surface)
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

// Unified Internal Commands
public class WorkshopModDownloadCommand : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
    public WorkshopModOperationType OperationType { get; init; }
}

public class WorkshopModCheckCommand : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
    public WorkshopModOperationType OperationType { get; init; }
}

public class WorkshopModExecuteCommand : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
    public WorkshopModOperationType OperationType { get; init; }
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

// Unified Events (Completions)
public class WorkshopModDownloadComplete : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
}

public class WorkshopModCheckComplete : IWorkshopModCommand
{
    public string WorkshopModId { get; init; }
    public bool InterventionRequired { get; init; }
    public List<string> AvailablePbos { get; init; }
}

public class WorkshopModExecuteComplete : IWorkshopModCommand
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
