namespace UKSF.Api.ArmaServer.Services;

public interface IArmaSyntheticLaunchGate
{
    bool TryAcquire(string runId);
    void Release();
    string CurrentRunId { get; }
}
