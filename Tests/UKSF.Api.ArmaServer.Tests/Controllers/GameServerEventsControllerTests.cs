using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class GameServerEventsControllerTests
{
    private readonly Mock<IGameServerEventHandler> _mockEventHandler = new();
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly GameServerEventsController _sut;

    public GameServerEventsControllerTests()
    {
        var scope = new Mock<IServiceScope>();
        var provider = new Mock<IServiceProvider>();
        provider.Setup(x => x.GetService(typeof(IGameServerEventHandler))).Returns(_mockEventHandler.Object);
        scope.Setup(x => x.ServiceProvider).Returns(provider.Object);
        _mockScopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);
        _sut = new GameServerEventsController(_mockEventHandler.Object, _mockScopeFactory.Object, _mockLogger.Object);
    }

    private void SetRequest(string body, int apiPort, string enqueueAt = "2026-05-06T12:00:00.000Z")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        httpContext.Request.Headers["X-Api-Port"] = apiPort.ToString();
        httpContext.Request.Headers["X-Enqueued-At"] = enqueueAt;
        _sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task ReceiveEvent_AcksImmediatelyAndDispatchesInBackground()
    {
        SetRequest(@"[""server_status"",[[""map"",""Altis""],[""players"",[""uid1""]]]]", 2303);

        var result = await _sut.ReceiveEvent();

        result.Should().BeOfType<AcceptedResult>();
        await Task.Delay(200);
        _mockEventHandler.Verify(
            x => x.HandleEventAsync(It.Is<GameServerEvent>(e => e.Type == "server_status" && e.ApiPort == 2303 && e.Data["map"].ToString() == "Altis")),
            Times.Once
        );
    }

    [Fact]
    public async Task ReceiveEvent_PersistenceSaveAwaitsHandlerBeforeAck()
    {
        SetRequest(@"[""persistence_save"",[[""sessionId"",""s1""]]]", 2303);

        var result = await _sut.ReceiveEvent();

        result.Should().BeOfType<OkResult>();
        _mockEventHandler.Verify(x => x.HandleEventAsync(It.Is<GameServerEvent>(e => e.Type == "persistence_save")), Times.Once);
    }

    [Fact]
    public async Task ReceiveEvent_InjectsEnqueueAtFromHeader()
    {
        SetRequest(@"[""mission_stats"",[[""sessionId"",""abc""]]]", 2303, "2026-05-06T13:30:00.000Z");

        await _sut.ReceiveEvent();
        await Task.Delay(200);

        _mockEventHandler.Verify(
            x => x.HandleEventAsync(
                It.Is<GameServerEvent>(e => e.Data.ContainsKey("enqueueAt") && e.Data["enqueueAt"].ToString() == "2026-05-06T13:30:00.000Z")
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ReceiveEvent_RejectsEmptyBody()
    {
        SetRequest("", 2303);

        var result = await _sut.ReceiveEvent();

        result.Should().BeOfType<BadRequestObjectResult>();
        _mockEventHandler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReceiveEvent_RejectsMissingApiPortHeader()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(@"[""server_status"",[]]"));
        _sut.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await _sut.ReceiveEvent();

        result.Should().BeOfType<BadRequestObjectResult>();
        _mockEventHandler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReceiveEvent_RejectsMalformedSqf()
    {
        SetRequest("not valid sqf", 2303);

        var result = await _sut.ReceiveEvent();

        result.Should().BeOfType<BadRequestObjectResult>();
        _mockEventHandler.VerifyNoOtherCalls();
    }
}
