using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.ScheduledActions;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.ScheduledActions;

public class ActionCleanupRunningServersTests
{
    private readonly Mock<IGameServerHelpers> _helpers = new();
    private readonly Mock<IGameServerProcessManager> _processManager = new();
    private readonly ActionCleanupRunningServers _action;

    public ActionCleanupRunningServersTests()
    {
        _helpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(1234, "arma3server_x64.exe")]);
        _action = new ActionCleanupRunningServers(
            Mock.Of<ISchedulerService>(),
            Mock.Of<IHostEnvironment>(),
            Mock.Of<IClock>(),
            Mock.Of<IUksfLogger>(),
            _helpers.Object,
            _processManager.Object
        );
    }

    [Theory]
    [InlineData(StopPhase.Ending)]
    [InlineData(StopPhase.Saving)]
    [InlineData(StopPhase.Delivering)]
    [InlineData(StopPhase.Stopping)]
    public async Task Run_WhileAnyServerIsStopping_KillsNothing(StopPhase phase)
    {
        var idle = new DomainGameServer { Id = "idle", Status = new GameServerStatus { Running = true } };
        var stopping = new DomainGameServer
        {
            Id = "stopping",
            Status = new GameServerStatus
            {
                Running = true,
                StopPhase = phase,
                StopPhaseEnteredAt = DateTime.UtcNow
            }
        };
        _processManager.Setup(x => x.GetAllServerStatusesAsync()).ReturnsAsync([idle, stopping]);

        await _action.Run();

        _processManager.Verify(x => x.KillServerAsync(It.IsAny<DomainGameServer>()), Times.Never);
        _processManager.Verify(x => x.KillAllAsync(), Times.Never);
    }

    [Fact]
    public async Task Run_WithOnlyIdleUnpopulatedServers_KillsThemAndTheRemainingProcesses()
    {
        var idle = new DomainGameServer { Id = "idle", Status = new GameServerStatus { Running = true } };
        _processManager.Setup(x => x.GetAllServerStatusesAsync()).ReturnsAsync([idle]);

        await _action.Run();

        _processManager.Verify(x => x.KillServerAsync(idle), Times.Once);
        _processManager.Verify(x => x.KillAllAsync(), Times.Once);
    }
}
