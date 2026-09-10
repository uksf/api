using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services;

public interface IGameServerProcessManager
{
    Task LaunchServerAsync(DomainGameServer server, string missionName, string launchedBy, int playerCount);
    Task StopServerAsync(DomainGameServer server);
    Task KillServerAsync(DomainGameServer server);
    Task<int> KillAllAsync();
    int GetInstanceCount();
    Task<List<DomainGameServer>> GetAllServerStatusesAsync();
    Task HandleStopEndingAsync(int apiPort);
    Task HandleStopSavingAsync(int apiPort);
    Task HandleStopStoppingAsync(int apiPort);
    Task HandleServerStatusAsync(int apiPort, Dictionary<string, object> data);
    Task PushServerUpdateAsync(DomainGameServer server);
    Task PushAllServersUpdateAsync();
    void EnsureMonitorRunning();
}
