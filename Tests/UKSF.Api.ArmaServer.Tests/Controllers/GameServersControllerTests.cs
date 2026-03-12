using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class GameServersControllerTests
{
    private readonly Mock<IGameServersContext> _mockGameServersContext = new();
    private readonly Mock<IVariablesContext> _mockVariablesContext = new();
    private readonly Mock<IGameServersService> _mockGameServersService = new();
    private readonly Mock<IMissionsService> _mockMissionsService = new();
    private readonly Mock<IHubContext<ServersHub, IServersClient>> _mockServersHub = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly Mock<IGameServerHelpers> _mockGameServerHelpers = new();
    private readonly Mock<IRptLogService> _mockRptLogService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IHttpContextService> _mockHttpContextService = new();
    private readonly GameServersController _sut;

    public GameServersControllerTests()
    {
        var mockClients = new Mock<IHubClients<IServersClient>>();
        var mockServersClient = new Mock<IServersClient>();
        mockClients.Setup(x => x.All).Returns(mockServersClient.Object);
        _mockServersHub.Setup(x => x.Clients).Returns(mockClients.Object);

        _sut = new GameServersController(
            _mockGameServersContext.Object,
            _mockVariablesContext.Object,
            _mockGameServersService.Object,
            _mockMissionsService.Object,
            _mockServersHub.Object,
            _mockVariablesService.Object,
            _mockGameServerHelpers.Object,
            _mockRptLogService.Object,
            _mockLogger.Object,
            _mockHttpContextService.Object
        );

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Hub-Connection-Id"] = "test-connection-id";
        _sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task KillServer_ShouldNotCallGetGameServerStatusAfterKill()
    {
        var serverId = "server1";
        var gameServer = new DomainGameServer { Id = serverId, Name = "Test Server" };

        _mockGameServersContext.Setup(x => x.GetSingle(serverId)).Returns(gameServer);
        _mockGameServersService.Setup(x => x.GetGameServerStatus(gameServer))
                               .Callback(() =>
                                   {
                                       gameServer.Status.Started = true;
                                       gameServer.Status.Running = true;
                                   }
                               )
                               .Returns(Task.CompletedTask);

        await _sut.KillServer(serverId);

        _mockGameServersService.Verify(x => x.GetGameServerStatus(gameServer), Times.Once);
    }

    [Fact]
    public async Task StopServer_ShouldNotCallGetGameServerStatusAfterStop()
    {
        var serverId = "server1";
        var gameServer = new DomainGameServer { Id = serverId, Name = "Test Server" };

        _mockGameServersContext.Setup(x => x.GetSingle(serverId)).Returns(gameServer);
        _mockGameServersService.Setup(x => x.GetGameServerStatus(gameServer))
                               .Callback(() =>
                                   {
                                       gameServer.Status.Started = true;
                                       gameServer.Status.Running = true;
                                   }
                               )
                               .Returns(Task.CompletedTask);

        await _sut.StopServer(serverId);

        _mockGameServersService.Verify(x => x.GetGameServerStatus(gameServer), Times.Once);
    }
}
