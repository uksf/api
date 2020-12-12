using System;
using Moq;
using UKSF.Api.Base.Events;
using UKSF.Api.EventHandlers;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;
using Xunit;

namespace UKSF.Tests.Unit.Events.Handlers {
    public class LogEventHandlerTests {
        private readonly IEventBus _eventBus;
        private readonly Mock<IAuditLogContext> _mockAuditLogDataService;
        private readonly Mock<IDiscordLogContext> _mockDiscordLogDataService;
        private readonly Mock<IHttpErrorLogContext> _mockHttpErrorLogDataService;
        private readonly Mock<ILauncherLogContext> _mockLauncherLogDataService;
        private readonly Mock<ILogContext> _mockLogDataService;
        private readonly Mock<IObjectIdConversionService> _mockObjectIdConversionService;

        public LogEventHandlerTests() {
            _mockLogDataService = new Mock<ILogContext>();
            _mockAuditLogDataService = new Mock<IAuditLogContext>();
            _mockHttpErrorLogDataService = new Mock<IHttpErrorLogContext>();
            _mockLauncherLogDataService = new Mock<ILauncherLogContext>();
            _mockDiscordLogDataService = new Mock<IDiscordLogContext>();
            _mockObjectIdConversionService = new Mock<IObjectIdConversionService>();
            Mock<ILogger> mockLogger = new();
            _eventBus = new EventBus();

            _mockObjectIdConversionService.Setup(x => x.ConvertObjectIds(It.IsAny<string>())).Returns<string>(x => x);

            LoggerEventHandler logEventHandler = new(
                _eventBus,
                _mockLogDataService.Object,
                _mockAuditLogDataService.Object,
                _mockHttpErrorLogDataService.Object,
                _mockLauncherLogDataService.Object,
                _mockDiscordLogDataService.Object,
                mockLogger.Object,
                _mockObjectIdConversionService.Object
            );
            logEventHandler.Init();
        }

        [Fact]
        public void When_handling_a_basic_log() {
            BasicLog basicLog = new("test");

            _eventBus.Send(basicLog);

            _mockObjectIdConversionService.Verify(x => x.ConvertObjectIds("test"), Times.Once);
            _mockLogDataService.Verify(x => x.Add(basicLog), Times.Once);
        }

        [Fact]
        public void When_handling_a_discord_log() {
            DiscordLog discordLog = new(DiscordUserEventType.JOINED, "12345", "SqnLdr.Beswick.T", "", "", "SqnLdr.Beswick.T joined");

            _eventBus.Send(discordLog);

            _mockDiscordLogDataService.Verify(x => x.Add(discordLog), Times.Once);
        }

        [Fact]
        public void When_handling_a_launcher_log() {
            LauncherLog launcherLog = new("1.0.0", "test");

            _eventBus.Send(launcherLog);

            _mockObjectIdConversionService.Verify(x => x.ConvertObjectIds("test"), Times.Once);
            _mockLauncherLogDataService.Verify(x => x.Add(launcherLog), Times.Once);
        }

        [Fact]
        public void When_handling_an_audit_log() {
            AuditLog basicLog = new("server", "test");

            _eventBus.Send(basicLog);

            _mockObjectIdConversionService.Verify(x => x.ConvertObjectId("server"), Times.Once);
            _mockObjectIdConversionService.Verify(x => x.ConvertObjectIds("test"), Times.Once);
            _mockAuditLogDataService.Verify(x => x.Add(basicLog), Times.Once);
        }

        [Fact]
        public void When_handling_an_http_error_log() {
            HttpErrorLog httpErrorLog = new(new Exception());

            _eventBus.Send(httpErrorLog);

            _mockObjectIdConversionService.Verify(x => x.ConvertObjectIds("Exception of type 'System.Exception' was thrown."), Times.Once);
            _mockHttpErrorLogDataService.Verify(x => x.Add(httpErrorLog), Times.Once);
        }
    }
}
