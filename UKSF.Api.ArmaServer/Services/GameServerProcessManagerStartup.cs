using UKSF.Api.ArmaServer.DataContext;

namespace UKSF.Api.ArmaServer.Services;

public class GameServerProcessManagerStartup(IGameServersContext gameServersContext, IGameServerProcessManager processManager) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (gameServersContext.Get().Any(s => s.ProcessId is not null))
        {
            processManager.EnsureMonitorRunning();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
