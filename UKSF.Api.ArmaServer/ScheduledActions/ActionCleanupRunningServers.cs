using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.ScheduledActions;

public interface IActionCleanupRunningServers : ISelfCreatingScheduledAction { }

public class ActionCleanupRunningServers : SelfCreatingScheduledAction, IActionCleanupRunningServers
{
    private const string ActionName = nameof(ActionCleanupRunningServers);
    private readonly IClock _clock;
    private readonly IGameServerHelpers _gameServerHelpers;
    private readonly IGameServersService _gameServersService;
    private readonly IUksfLogger _logger;

    public ActionCleanupRunningServers(
        ISchedulerService schedulerService,
        ISchedulerContext schedulerContext,
        IHostEnvironment currentEnvironment,
        IClock clock,
        IUksfLogger logger,
        IGameServersService gameServersService,
        IGameServerHelpers gameServerHelpers
    ) : base(schedulerService, schedulerContext, currentEnvironment)
    {
        _clock = clock;
        _logger = logger;
        _gameServersService = gameServersService;
        _gameServerHelpers = gameServerHelpers;
    }

    public override DateTime NextRun => _clock.UkToday().AddHours(02);
    public override TimeSpan RunInterval => TimeSpan.FromDays(1);
    public override string Name => ActionName;

    public override async Task Run(params object[] parameters)
    {
        var armaProcesses = _gameServerHelpers.GetArmaProcesses();
        if (!armaProcesses.Any())
        {
            // No arma processes running, don't need to do anything
            return;
        }

        var serverStatuses = await _gameServersService.GetAllGameServerStatuses();
        var runningServers = serverStatuses.Where(x => x.Status.Running).ToList();
        var populatedServers = runningServers.Where(x => x.Status.Players > 0);

        if (populatedServers.Any())
        {
            // There are populated servers, don't kill anything
            return;
        }

        KillOrphanedServers(runningServers);
        KillRemainingProcesses();
    }

    private void KillOrphanedServers(List<GameServer> runningServers)
    {
        var killedCount = 0;
        foreach (var runningServer in runningServers)
        {
            try
            {
                _gameServersService.KillGameServer(runningServer);
                killedCount++;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    $"Failed to kill running game server - Name: {runningServer.Name} - ProcessId: {runningServer.ProcessId}\n{exception.GetCompleteString()}"
                );
            }
        }

        _logger.LogInfo($"Killed {killedCount} leftover servers");
    }

    private void KillRemainingProcesses()
    {
        try
        {
            var killedCount = _gameServersService.KillAllArmaProcesses();
            if (killedCount > 0)
            {
                _logger.LogInfo($"Killed {killedCount} orphaned arma processes");
            }
        }
        catch (Exception exception)
        {
            _logger.LogError($"Failed to kill arma processes\n{exception.GetCompleteString()}");
        }
    }
}
