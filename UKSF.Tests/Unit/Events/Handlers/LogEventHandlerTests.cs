using System;
using System.Reactive.Subjects;
using Moq;
using UKSF.Api.EventHandlers;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;
using Xunit;

namespace UKSF.Tests.Unit.Events.Handlers {
    public class LogEventHandlerTests {
        private readonly Subject<BasicLog> _loggerSubject = new Subject<BasicLog>();
        private readonly Mock<IAuditLogDataService> _mockAuditLogDataService;
        private readonly Mock<IHttpErrorLogDataService> _mockHttpErrorLogDataService;
        private readonly Mock<ILauncherLogDataService> _mockLauncherLogDataService;
        private readonly Mock<ILogDataService> _mockLogDataService;
        private readonly Mock<IObjectIdConversionService> _mockObjectIdConversionService;

        public LogEventHandlerTests() {
            _mockLogDataService = new Mock<ILogDataService>();
            _mockAuditLogDataService = new Mock<IAuditLogDataService>();
            _mockHttpErrorLogDataService = new Mock<IHttpErrorLogDataService>();
            _mockLauncherLogDataService = new Mock<ILauncherLogDataService>();
            _mockObjectIdConversionService = new Mock<IObjectIdConversionService>();

            Mock<ILogger> mockLogger = new Mock<ILogger>();
            mockLogger.Setup(x => x.AsObservable()).Returns(_loggerSubject);
            _mockObjectIdConversionService.Setup(x => x.ConvertObjectIds(It.IsAny<string>())).Returns<string>(x => x);

            LoggerEventHandler logEventHandler = new LoggerEventHandler(
                _mockLogDataService.Object,
                _mockAuditLogDataService.Object,
                _mockHttpErrorLogDataService.Object,
                _mockLauncherLogDataService.Object,
                mockLogger.Object,
                _mockObjectIdConversionService.Object
            );
            logEventHandler.Init();
        }

        [Fact]
        public void When_handling_a_basic_log() {
            BasicLog basicLog = new BasicLog("test");

            _loggerSubject.OnNext(basicLog);

            _mockObjectIdConversionService.Verify(x => x.ConvertObjectIds("test"), Times.Once);
            _mockLogDataService.Verify(x => x.Add(basicLog), Times.Once);
        }

        [Fact]
        public void When_handling_a_launcher_log() {
            LauncherLog launcherLog = new LauncherLog("1.0.0", "test");

            _loggerSubject.OnNext(launcherLog);

            _mockObjectIdConversionService.Verify(x => x.ConvertObjectIds("test"), Times.Once);
            _mockLauncherLogDataService.Verify(x => x.Add(launcherLog), Times.Once);
        }

        [Fact]
        public void When_handling_an_audit_log() {
            AuditLog basicLog = new AuditLog("server", "test");

            _loggerSubject.OnNext(basicLog);

            _mockObjectIdConversionService.Verify(x => x.ConvertObjectId("server"), Times.Once);
            _mockObjectIdConversionService.Verify(x => x.ConvertObjectIds("test"), Times.Once);
            _mockAuditLogDataService.Verify(x => x.Add(basicLog), Times.Once);
        }

        [Fact]
        public void When_handling_an_http_error_log() {
            HttpErrorLog httpErrorLog = new HttpErrorLog(new Exception());

            _loggerSubject.OnNext(httpErrorLog);

            _mockObjectIdConversionService.Verify(x => x.ConvertObjectIds("Exception of type 'System.Exception' was thrown."), Times.Once);
            _mockHttpErrorLogDataService.Verify(x => x.Add(httpErrorLog), Times.Once);
        }
    }
}
