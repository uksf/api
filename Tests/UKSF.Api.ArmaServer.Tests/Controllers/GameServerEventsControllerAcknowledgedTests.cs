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

public class GameServerEventsControllerAcknowledgedTests
{
    private readonly Mock<IGameServerEventHandler> _handler = new();
    private readonly GameServerEventsController _sut;

    public GameServerEventsControllerAcknowledgedTests()
    {
        _sut = new GameServerEventsController(_handler.Object, Mock.Of<IServiceScopeFactory>(), Mock.Of<IUksfLogger>());
    }

    private void SetRequest(string type)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes($@"[""{type}"",[[""sessionId"",""s1""]]]"));
        httpContext.Request.Headers["X-Api-Port"] = "2303";
        httpContext.Request.Headers["X-Uksf-Protocol"] = "2";
        _sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Theory]
    [InlineData("mission_ended")]
    [InlineData("shutdown_ending")]
    [InlineData("shutdown_saving")]
    [InlineData("shutdown_delivering")]
    [InlineData("shutdown_stopping")]
    public async Task AcknowledgedEvent_IsHandledBeforeTheResponse(string type)
    {
        SetRequest(type);

        var result = await _sut.ReceiveEvent();

        result.Should().BeOfType<OkResult>();
        _handler.Verify(x => x.HandleEventAsync(It.Is<GameServerEvent>(e => e.Type == type)), Times.Once);
    }

    [Theory]
    [InlineData("mission_ended")]
    [InlineData("shutdown_stopping")]
    public async Task AcknowledgedEvent_WhenHandlingFails_ReturnsAServerErrorSoTheExtensionRetries(string type)
    {
        SetRequest(type);
        _handler.Setup(x => x.HandleEventAsync(It.IsAny<GameServerEvent>())).ThrowsAsync(new TimeoutException("mongo"));

        var result = await _sut.ReceiveEvent();

        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }
}
