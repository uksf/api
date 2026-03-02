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
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class GameServerEventsControllerTests
{
    private readonly Mock<IGameServersService> _mockService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly GameServerEventsController _sut;

    public GameServerEventsControllerTests()
    {
        _sut = new GameServerEventsController(_mockService.Object, _mockLogger.Object);
        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    [Fact]
    public async Task ReceiveEvent_DelegatesToGameServersService()
    {
        var gameServerEvent = new GameServerEvent { Type = "server_status", Data = new Dictionary<string, object> { { "players", "5" } } };

        var result = await _sut.ReceiveEvent(gameServerEvent);

        result.Should().BeOfType<OkResult>();
        _mockService.Verify(x => x.HandleGameServerEvent(gameServerEvent), Times.Once);
    }

    [Fact]
    public async Task ReceiveEvent_LogsDebugMessage()
    {
        var gameServerEvent = new GameServerEvent { Type = "persistence_save", Data = new Dictionary<string, object>() };

        await _sut.ReceiveEvent(gameServerEvent);

        _mockLogger.Verify(x => x.LogDebug(It.Is<string>(s => s.Contains("persistence_save"))), Times.Once);
    }
}
