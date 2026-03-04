using System;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Core.Signalr;
using Xunit;

namespace UKSF.Api.Core.Tests.Signalr;

public class HubExceptionFilterTests
{
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly HubExceptionFilter _filter;

    public HubExceptionFilterTests()
    {
        _filter = new HubExceptionFilter(_mockLogger.Object);
    }

    [Fact]
    public async Task InvokeMethodAsync_WhenNextSucceeds_ReturnsResult()
    {
        var expectedResult = new object();
        var context = CreateInvocationContext("TestHub", "TestMethod");

        var result = await _filter.InvokeMethodAsync(context, _ => new ValueTask<object>(expectedResult));

        result.Should().Be(expectedResult);
        _mockLogger.Verify(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
    }

    [Fact]
    public async Task InvokeMethodAsync_WhenNextThrowsHubException_RethrowsWithoutLogging()
    {
        var hubException = new HubException("Already a hub exception");
        var context = CreateInvocationContext("TestHub", "TestMethod");

        var act = () => _filter.InvokeMethodAsync(context, _ => throw hubException).AsTask();

        await act.Should().ThrowAsync<HubException>().WithMessage("Already a hub exception");
        _mockLogger.Verify(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
    }

    [Fact]
    public async Task InvokeMethodAsync_WhenNextThrowsException_LogsAndThrowsHubException()
    {
        var originalException = new InvalidOperationException("Something broke");
        var context = CreateInvocationContext("ServersHub", "SubscribeToLog");

        var act = () => _filter.InvokeMethodAsync(context, _ => throw originalException).AsTask();

        await act.Should().ThrowAsync<HubException>().WithMessage("TestServersHub.SubscribeToLog failed: Something broke");
        _mockLogger.Verify(x => x.LogError("Unhandled exception in TestServersHub.SubscribeToLog", originalException), Times.Once);
    }

    [Fact]
    public async Task InvokeMethodAsync_WhenNextThrowsNullReferenceException_LogsWithHubAndMethodName()
    {
        var nre = new NullReferenceException("Object reference not set");
        var context = CreateInvocationContext("ServersHub", "SubscribeToLog");

        var act = () => _filter.InvokeMethodAsync(context, _ => throw nre).AsTask();

        await act.Should().ThrowAsync<HubException>().WithMessage("TestServersHub.SubscribeToLog failed: Object reference not set");
        _mockLogger.Verify(x => x.LogError("Unhandled exception in TestServersHub.SubscribeToLog", nre), Times.Once);
    }

    private static HubInvocationContext CreateInvocationContext(string hubTypeName, string methodName)
    {
        var hubType = hubTypeName switch
        {
            "ServersHub" => typeof(TestServersHub),
            _            => typeof(TestHub)
        };

        var hub = (Hub)Activator.CreateInstance(hubType)!;
        var mockCallerContext = new Mock<HubCallerContext>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var methodInfo = hubType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance) ??
                         typeof(Hub).GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance)!;

        return new HubInvocationContext(mockCallerContext.Object, mockServiceProvider.Object, hub, methodInfo, []);
    }

    private class TestHub : Hub
    {
        public void TestMethod() { }
    }

    private class TestServersHub : Hub
    {
        public void SubscribeToLog() { }
    }
}
