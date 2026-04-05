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
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class GameServersControllerTests
{
    private readonly Mock<IGameServersService> _mockGameServersService = new();
    private readonly Mock<IGameServerProcessManager> _mockProcessManager = new();
    private readonly Mock<IMissionsService> _mockMissionsService = new();
    private readonly Mock<IRptLogService> _mockRptLogService = new();
    private readonly Mock<IGameServerHelpers> _mockGameServerHelpers = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IHttpContextService> _mockHttpContextService = new();
    private readonly GameServersController _sut;

    public GameServersControllerTests()
    {
        _sut = new GameServersController(
            _mockGameServersService.Object,
            _mockProcessManager.Object,
            _mockMissionsService.Object,
            _mockRptLogService.Object,
            _mockGameServerHelpers.Object,
            _mockLogger.Object,
            _mockHttpContextService.Object
        );

        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    [Fact]
    public void GetGameServers_ShouldReturnGameServersUpdate()
    {
        var servers = new List<DomainGameServer> { new() { Id = "s1", Name = "Test" } };
        _mockGameServersService.Setup(x => x.GetServers()).Returns(servers);
        _mockMissionsService.Setup(x => x.GetActiveMissions()).Returns([]);
        _mockProcessManager.Setup(x => x.GetInstanceCount()).Returns(1);

        var result = _sut.GetGameServers();

        result.Servers.Should().HaveCount(1);
        result.InstanceCount.Should().Be(1);
        result.Missions.Should().NotBeNull();
    }

    [Fact]
    public async Task StopServer_ShouldCallProcessManagerStopServerAsync()
    {
        var gameServer = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            Status = new GameServerStatus { Running = true }
        };
        _mockGameServersService.Setup(x => x.GetServer("s1")).Returns(gameServer);

        await _sut.StopServer("s1");

        _mockProcessManager.Verify(x => x.StopServerAsync(gameServer), Times.Once);
    }

    [Fact]
    public async Task KillServer_ShouldCallProcessManagerKillServerAsync()
    {
        var gameServer = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            Status = new GameServerStatus { Running = true },
            ProcessId = 1234
        };
        _mockGameServersService.Setup(x => x.GetServer("s1")).Returns(gameServer);

        await _sut.KillServer("s1");

        _mockProcessManager.Verify(x => x.KillServerAsync(gameServer), Times.Once);
    }

    [Fact]
    public async Task AddServer_ShouldAddServerAndPushAllServersUpdate()
    {
        var gameServer = new DomainGameServer { Id = "s1", Name = "New Server" };

        await _sut.AddServer(gameServer);

        _mockGameServersService.Verify(x => x.AddServerAsync(gameServer), Times.Once);
        _mockProcessManager.Verify(x => x.PushAllServersUpdateAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteServer_ShouldDeleteServerAndPushAllServersUpdate()
    {
        await _sut.DeleteGameServer("s1");

        _mockGameServersService.Verify(x => x.DeleteServerAsync("s1"), Times.Once);
        _mockProcessManager.Verify(x => x.PushAllServersUpdateAsync(), Times.Once);
    }

    [Fact]
    public async Task EditGameServer_ShouldEditServerAndPushServerUpdate()
    {
        var gameServer = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            Environment = GameEnvironment.Release
        };
        var updatedServer = new DomainGameServer { Id = "s1", Name = "Test" };
        _mockGameServersService.Setup(x => x.GetServer("s1")).Returns(updatedServer);

        await _sut.EditGameServer(gameServer);

        _mockGameServersService.Verify(x => x.EditServerAsync(gameServer), Times.Once);
        _mockProcessManager.Verify(x => x.PushServerUpdateAsync(updatedServer), Times.Once);
    }
}
