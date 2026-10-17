using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class GameServersControllerKillGuardTests
{
    private readonly Mock<IGameServersService> _mockGameServersService = new();
    private readonly Mock<IGameServerProcessManager> _mockProcessManager = new();
    private readonly Mock<IMissionsService> _mockMissionsService = new();
    private readonly Mock<IRptLogService> _mockRptLogService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IHttpContextService> _mockHttpContextService = new();
    private readonly GameServersController _sut;

    public GameServersControllerKillGuardTests()
    {
        _sut = new GameServersController(
            _mockGameServersService.Object,
            _mockProcessManager.Object,
            _mockMissionsService.Object,
            _mockRptLogService.Object,
            _mockLogger.Object,
            _mockHttpContextService.Object,
            Mock.Of<IGameServerLaunchService>()
        );

        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    [Fact]
    public async Task KillServer_WhileStoppingNormally_ThrowsAndDoesNotKill()
    {
        var gameServer = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = 1234,
            Status = new GameServerStatus
            {
                StopPhase = StopPhase.Stopping,
                StopPhaseEnteredAt = DateTime.UtcNow,
                KillAllowedAt = DateTime.UtcNow.AddSeconds(20)
            }
        };
        _mockGameServersService.Setup(x => x.GetServer("s1")).Returns(gameServer);

        var act = () => _sut.KillServer("s1");

        await act.Should().ThrowAsync<BadRequestException>();
        _mockProcessManager.Verify(x => x.KillServerAsync(It.IsAny<DomainGameServer>()), Times.Never);
    }

    [Fact]
    public async Task KillServer_WhenStopExceedsNormalTime_Kills()
    {
        var gameServer = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = 1234,
            Status = new GameServerStatus
            {
                StopPhase = StopPhase.Stopping,
                StopPhaseEnteredAt = DateTime.UtcNow.AddSeconds(-30),
                KillAllowedAt = DateTime.UtcNow.AddSeconds(-10)
            }
        };
        _mockGameServersService.Setup(x => x.GetServer("s1")).Returns(gameServer);

        await _sut.KillServer("s1");

        _mockProcessManager.Verify(x => x.KillServerAsync(gameServer), Times.Once);
    }

    [Theory]
    [InlineData(StopPhase.Ending)]
    [InlineData(StopPhase.Saving)]
    [InlineData(StopPhase.Delivering)]
    public async Task KillServer_BeforeTheSaveIsStored_WithoutDataLossConfirmation_ThrowsAndDoesNotKill(StopPhase phase)
    {
        var gameServer = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = 1234,
            Status = new GameServerStatus
            {
                StopPhase = phase,
                KillAllowedAt = DateTime.UtcNow.AddSeconds(-1),
                CurrentMissionSessionId = "sess-1"
            }
        };
        _mockGameServersService.Setup(x => x.GetServer("s1")).Returns(gameServer);

        var act = () => _sut.KillServer("s1");

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*persistence data*");
        _mockProcessManager.Verify(x => x.KillServerAsync(It.IsAny<DomainGameServer>()), Times.Never);
    }

    [Fact]
    public async Task KillServer_BeforeTheSaveIsStored_WithDataLossConfirmation_Kills()
    {
        var gameServer = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = 1234,
            Status = new GameServerStatus
            {
                StopPhase = StopPhase.Saving,
                KillAllowedAt = DateTime.UtcNow.AddSeconds(-1),
                CurrentMissionSessionId = "sess-1"
            }
        };
        _mockGameServersService.Setup(x => x.GetServer("s1")).Returns(gameServer);

        await _sut.KillServer("s1", confirmDataLoss: true);

        _mockProcessManager.Verify(x => x.KillServerAsync(gameServer, true), Times.Once);
    }

    [Fact]
    public async Task KillAll_WhileAServerIsSaving_WithoutDataLossConfirmation_ThrowsAndDoesNotKill()
    {
        _mockGameServersService.Setup(x => x.GetServers())
                               .Returns(
                                   [
                                       new DomainGameServer
                                       {
                                           Name = "Saving",
                                           Status = new GameServerStatus { StopPhase = StopPhase.Delivering, CurrentMissionSessionId = "sess-1" }
                                       }
                                   ]
                               );

        var act = () => _sut.KillAllArmaProcesses();

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Saving*persistence data*");
        _mockProcessManager.Verify(x => x.KillAllAsync(), Times.Never);
    }

    [Fact]
    public async Task KillAll_WhileAServerIsSaving_WithDataLossConfirmation_Kills()
    {
        _mockGameServersService.Setup(x => x.GetServers())
                               .Returns(
                                   [
                                       new DomainGameServer
                                       {
                                           Name = "Saving", Status = new GameServerStatus { StopPhase = StopPhase.Saving, CurrentMissionSessionId = "sess-1" }
                                       }
                                   ]
                               );

        await _sut.KillAllArmaProcesses(confirmDataLoss: true);

        _mockProcessManager.Verify(x => x.KillAllAsync(true), Times.Once);
    }

    [Fact]
    public async Task KillAll_WhenNoServerIsSaving_KillsWithoutConfirmation()
    {
        _mockGameServersService.Setup(x => x.GetServers())
                               .Returns([new DomainGameServer { Name = "Stopping", Status = new GameServerStatus { StopPhase = StopPhase.Stopping } }]);

        await _sut.KillAllArmaProcesses();

        _mockProcessManager.Verify(x => x.KillAllAsync(), Times.Once);
    }

    [Fact]
    public async Task KillServer_WhenStuckEndingWithoutAMission_KillsAtOnceWithoutConfirmation()
    {
        var gameServer = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = 1234,
            Status = new GameServerStatus
            {
                Running = true,
                StopPhase = StopPhase.Ending,
                KillAllowedAt = DateTime.UtcNow.AddSeconds(30)
            }
        };
        _mockGameServersService.Setup(x => x.GetServer("s1")).Returns(gameServer);

        await _sut.KillServer("s1");

        _mockProcessManager.Verify(x => x.KillServerAsync(gameServer, false), Times.Once);
    }

    [Fact]
    public async Task KillAll_WhenAServerIsEndingWithoutAMission_KillsWithoutConfirmation()
    {
        _mockGameServersService.Setup(x => x.GetServers())
                               .Returns([new DomainGameServer { Name = "Unloaded", Status = new GameServerStatus { StopPhase = StopPhase.Ending } }]);

        await _sut.KillAllArmaProcesses();

        _mockProcessManager.Verify(x => x.KillAllAsync(false), Times.Once);
    }
}
