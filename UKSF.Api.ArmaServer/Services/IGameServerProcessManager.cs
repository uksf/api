using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services;

public interface IGameServerProcessManager
{
    Task LaunchServerAsync(DomainGameServer server, string missionName, string launchedBy, int playerCount);
    Task StopServerAsync(DomainGameServer server);
    Task KillServerAsync(DomainGameServer server, bool confirmDataLoss = false);
    Task<int> KillAllAsync(bool confirmDataLoss = false);
    int GetInstanceCount();
    Task<List<DomainGameServer>> GetAllServerStatusesAsync();
    Task HandleStopEndingAsync(int apiPort, DateTime? enqueuedAt = null);
    Task HandleStopSavingAsync(int apiPort, DateTime? enqueuedAt = null);
    Task HandleStopDeliveringAsync(int apiPort, DateTime? enqueuedAt = null);
    Task HandleStopStoppingAsync(int apiPort, DateTime? enqueuedAt = null);
    Task<bool> RecordMissionSessionAsync(int apiPort, string sessionId, bool started, DateTime? enqueuedAt = null);
    Task HandleServerStatusAsync(int apiPort, Dictionary<string, object> data);
    Task PushServerUpdateAsync(DomainGameServer server);
    Task PushAllServersUpdateAsync();
    void EnsureMonitorRunning();
}
