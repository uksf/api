namespace UKSF.Api.Modpack.Models;

public record EmergencyCleanupResult
{
    public int ProcessesKilled { get; init; }
    public int BuildsCancelled { get; init; }
    public string Message => $"Emergency cleanup completed. Killed {ProcessesKilled} processes and cancelled {BuildsCancelled} builds.";
}
