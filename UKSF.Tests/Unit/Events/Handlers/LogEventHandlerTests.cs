using System;
using System.Reactive.Subjects;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Admin.Signalr.Clients;
using UKSF.Api.Admin.Signalr.Hubs;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Base.Models.Logging;
using UKSF.Api.EventHandlers;
using UKSF.Api.Personnel.Services;
using Xunit;

namespace UKSF.Tests.Unit.Events.Handlers {
    public class LogEventHandlerTests {
        private readonly LoggerEventHandler logEventHandler;
        private readonly Mock<ILogDataService> mockLogDataService;
        private readonly Mock<ILogger> mockLogger;
        private readonly Subject<BasicLog> loggerSubject = new Subject<BasicLog>();

        public LogEventHandlerTests() {
            mockLogDataService = new Mock<ILogDataService>();
            mockLogger = new Mock<ILogger>();
            Mock<IObjectIdConversionService> mockObjectIdConversionService = new Mock<IObjectIdConversionService>();

            mockLogger.Setup(x => x.AsObservable()).Returns(loggerSubject);
            mockObjectIdConversionService.Setup(x => x.ConvertObjectIds(It.IsAny<string>())).Returns<string>(x => x);

            logEventHandler = new LoggerEventHandler(mockLogDataService.Object, mockLogger.Object, mockObjectIdConversionService.Object);
            logEventHandler.Init();
        }

        [Fact]
        public void ShouldLogOnException() {
            mockLogger.Setup(x => x.LogError(It.IsAny<Exception>()));

            loggerSubject.OnNext(new HttpErrorLog(new Exception()));

            mockLogDataService.Verify(x => x.Add(It.IsAny<HttpErrorLog>()), Times.Once);
        }

        [Fact]
        public void ShouldRunAddedOnAddWithCorrectType() {
            Mock<IHubClients<IAdminClient>> mockHubClients = new Mock<IHubClients<IAdminClient>>();
            Mock<IAdminClient> mockClient = new Mock<IAdminClient>();

            mockHubClients.Setup(x => x.All).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveAuditLog(It.IsAny<AuditLog>()));
            mockClient.Setup(x => x.ReceiveLauncherLog(It.IsAny<LauncherLog>()));
            mockClient.Setup(x => x.ReceiveErrorLog(It.IsAny<HttpErrorLog>()));
            mockClient.Setup(x => x.ReceiveLog(It.IsAny<BasicLog>()));

            loggerSubject.OnNext(new AuditLog("server", "test"));
            loggerSubject.OnNext(new LauncherLog("1.0.0", "test"));
            loggerSubject.OnNext(new HttpErrorLog(new Exception("test")));
            loggerSubject.OnNext(new BasicLog("test"));

            mockClient.Verify(x => x.ReceiveAuditLog(It.IsAny<AuditLog>()), Times.Once);
            mockClient.Verify(x => x.ReceiveLauncherLog(It.IsAny<LauncherLog>()), Times.Once);
            mockClient.Verify(x => x.ReceiveErrorLog(It.IsAny<HttpErrorLog>()), Times.Once);
            mockClient.Verify(x => x.ReceiveLog(It.IsAny<BasicLog>()), Times.Once);
        }
    }
}
