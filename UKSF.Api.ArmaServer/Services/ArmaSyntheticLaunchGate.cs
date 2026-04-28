namespace UKSF.Api.ArmaServer.Services;

public class ArmaSyntheticLaunchGate : IArmaSyntheticLaunchGate
{
    private readonly object _lock = new();
    private string _currentRunId;

    public bool TryAcquire(string runId)
    {
        lock (_lock)
        {
            if (_currentRunId is not null) return false;
            _currentRunId = runId;
            return true;
        }
    }

    public void Release()
    {
        lock (_lock) _currentRunId = null;
    }

    public string CurrentRunId
    {
        get
        {
            lock (_lock) return _currentRunId;
        }
    }
}
