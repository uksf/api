using System;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.EventHandlers;
using Xunit;

namespace UKSF.Tests.Unit.Events.Handlers;

public class LogEventHandlerTests
{
    private readonly IEventBus _eventBus;
    private readonly Mock<IAuditLogContext> _mockAuditLogDataService;
    private readonly Mock<IDiscordLogContext> _mockDiscordLogDataService;
    private readonly Mock<IErrorLogContext> _mockErrorLogDataService;
    private readonly Mock<ILauncherLogContext> _mockLauncherLogDataService;
    private readonly Mock<ILogContext> _mockLogDataService;
    private readonly Mock<IObjectIdConversionService> _mockObjectIdConversionService;

    public LogEventHandlerTests()
    {
        _mockLogDataService = new Mock<ILogContext>();
        _mockAuditLogDataService = new Mock<IAuditLogContext>();
        _mockErrorLogDataService = new Mock<IErrorLogContext>();
        _mockLauncherLogDataService = new Mock<ILauncherLogContext>();
        _mockDiscordLogDataService = new Mock<IDiscordLogContext>();
        _mockObjectIdConversionService = new Mock<IObjectIdConversionService>();
        Mock<IUksfLogger> mockLogger = new();
        _eventBus = new EventBus();

        _mockObjectIdConversionService.Setup(x => x.ConvertObjectIds(It.IsAny<string>())).Returns<string>(x => x);

        UksfLoggerEventHandler logEventHandler = new(
            _eventBus,
            _mockLogDataService.Object,
            _mockAuditLogDataService.Object,
            _mockErrorLogDataService.Object,
            _mockLauncherLogDataService.Object,
            _mockDiscordLogDataService.Object,
            mockLogger.Object,
            _mockObjectIdConversionService.Object
        );
        logEventHandler.EarlyInit();
    }

    [Fact]
    public void When_handling_a_basic_log()
    {
        DomainBasicLog basicLog = new("test");

        _eventBus.Send(new LoggerEventData(basicLog), "");

        _mockObjectIdConversionService.Verify(x => x.ConvertObjectIds("test"), Times.Once);
        _mockLogDataService.Verify(x => x.Add(basicLog), Times.Once);
    }

    [Fact]
    public void When_handling_a_discord_log()
    {
        DiscordLog discordLog = new(DiscordUserEventType.Joined, "12345", "SqnLdr.Beswick.T", "", "", "SqnLdr.Beswick.T joined");

        _eventBus.Send(new LoggerEventData(discordLog), "");

        _mockDiscordLogDataService.Verify(x => x.Add(discordLog), Times.Once);
    }

    [Fact]
    public void When_handling_a_launcher_log()
    {
        LauncherLog launcherLog = new("1.0.0", "test");

        _eventBus.Send(new LoggerEventData(launcherLog), "");

        _mockObjectIdConversionService.Verify(x => x.ConvertObjectIds("test"), Times.Once);
        _mockLauncherLogDataService.Verify(x => x.Add(launcherLog), Times.Once);
    }

    [Fact]
    public void When_handling_an_audit_log()
    {
        AuditLog basicLog = new("server", "test");

        _eventBus.Send(new LoggerEventData(basicLog), "");

        _mockObjectIdConversionService.Verify(x => x.ConvertObjectId("server"), Times.Once);
        _mockObjectIdConversionService.Verify(x => x.ConvertObjectIds("test"), Times.Once);
        _mockAuditLogDataService.Verify(x => x.Add(basicLog), Times.Once);
    }

    [Fact]
    public void When_handling_an_error_log()
    {
        ErrorLog errorLog = new(new Exception(), "url", "method", "endpoint", 500, "userId", "userName");

        _eventBus.Send(new LoggerEventData(errorLog), "");

        _mockObjectIdConversionService.Verify(x => x.ConvertObjectIds("Exception of type 'System.Exception' was thrown."), Times.Once);
        _mockErrorLogDataService.Verify(x => x.Add(errorLog), Times.Once);
    }
}
