namespace UKSF.Api.Core.Processes;

/// <summary>
/// Generic interface for tracking processes
/// </summary>
public interface IProcessTracker
{
    /// <summary>
    /// Register a process for tracking
    /// </summary>
    void RegisterProcess(int processId, string trackingId, string description);

    /// <summary>
    /// Unregister a process from tracking
    /// </summary>
    void UnregisterProcess(int processId);
}
