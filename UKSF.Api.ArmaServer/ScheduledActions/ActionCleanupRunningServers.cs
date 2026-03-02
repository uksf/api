using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.ScheduledActions;

public interface IActionCleanupRunningServers : ISelfCreatingScheduledAction;

public class ActionCleanupRunningServers(
    ISchedulerService schedulerService,
    IHostEnvironment currentEnvironment,
    IClock clock,
    IUksfLogger logger,
    IServiceScopeFactory serviceScopeFactory,
    IGameServerHelpers gameServerHelpers
) : SelfCreatingScheduledAction(schedulerService, currentEnvironment), IActionCleanupRunningServers
{
    private const string ActionName = nameof(ActionCleanupRunningServers);

    public override DateTime NextRun => clock.UkToday().AddHours(02);
    public override TimeSpan RunInterval => TimeSpan.FromDays(1);
    public override string Name => ActionName;

    public override async Task Run(params object[] parameters)
    {
        var armaProcesses = gameServerHelpers.GetArmaProcesses();
        if (!armaProcesses.Any())
        {
            return;
        }

        using var scope = serviceScopeFactory.CreateScope();
        var gameServersService = scope.ServiceProvider.GetRequiredService<IGameServersService>();

        var serverStatuses = await gameServersService.GetAllGameServerStatuses();
        var runningServers = serverStatuses.Where(x => x.Status.Running).ToList();
        var populatedServers = runningServers.Where(x => x.Status.Players > 0);

        if (populatedServers.Any())
        {
            return;
        }

        await KillOrphanedServersAsync(gameServersService, runningServers);
        await KillRemainingProcesses(gameServersService);
    }

    private async Task KillOrphanedServersAsync(IGameServersService gameServersService, List<DomainGameServer> runningServers)
    {
        var killedCount = 0;
        foreach (var runningServer in runningServers)
        {
            try
            {
                await gameServersService.KillGameServer(runningServer);
                killedCount++;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    $"Failed to kill running game server - Name: {runningServer.Name} - ProcessId: {runningServer.ProcessId}\n{exception.GetCompleteString()}"
                );
            }
        }

        if (killedCount > 0)
        {
            logger.LogInfo($"Killed {killedCount} leftover servers");
        }
    }

    private async Task KillRemainingProcesses(IGameServersService gameServersService)
    {
        try
        {
            var killedCount = await gameServersService.KillAllArmaProcesses();
            if (killedCount > 0)
            {
                logger.LogInfo($"Killed {killedCount} orphaned arma processes");
            }
        }
        catch (Exception exception)
        {
            logger.LogError($"Failed to kill arma processes\n{exception.GetCompleteString()}");
        }
    }
}
