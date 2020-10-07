using System;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Events.Data;
using UKSF.Api.Events.Handlers;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Message.Logging;
using UKSF.Api.Signalr.Hubs.Utility;
using Xunit;

namespace UKSF.Tests.Unit.Events.Handlers {
    public class LogEventHandlerTests {
        private readonly DataEventBus<BasicLogMessage> dataEventBus;
        private readonly LogEventHandler logEventHandler;
        private readonly Mock<IHubContext<AdminHub, IAdminClient>> mockHub;
        private readonly Mock<ILoggingService> mockLoggingService;

        public LogEventHandlerTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            mockHub = new Mock<IHubContext<AdminHub, IAdminClient>>();
            mockLoggingService = new Mock<ILoggingService>();

            dataEventBus = new DataEventBus<BasicLogMessage>();

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<BasicLogMessage>(It.IsAny<string>()));

            logEventHandler = new LogEventHandler(dataEventBus, mockHub.Object, mockLoggingService.Object);
        }

        [Fact]
        public void ShouldLogOnException() {
            mockLoggingService.Setup(x => x.Log(It.IsAny<Exception>()));

            logEventHandler.Init();

            dataEventBus.Send(new DataEventModel<BasicLogMessage> { type = DataEventType.ADD, data = new object() });

            mockLoggingService.Verify(x => x.Log(It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void ShouldNotRunEventOnUpdateOrDelete() {
            Mock<IHubClients<IAdminClient>> mockHubClients = new Mock<IHubClients<IAdminClient>>();
            Mock<IAdminClient> mockClient = new Mock<IAdminClient>();

            mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.All).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveAuditLog(It.IsAny<AuditLogMessage>()));
            mockClient.Setup(x => x.ReceiveLauncherLog(It.IsAny<LauncherLogMessage>()));
            mockClient.Setup(x => x.ReceiveErrorLog(It.IsAny<WebLogMessage>()));
            mockClient.Setup(x => x.ReceiveLog(It.IsAny<BasicLogMessage>()));

            logEventHandler.Init();

            dataEventBus.Send(new DataEventModel<BasicLogMessage> { type = DataEventType.UPDATE, data = new BasicLogMessage("test") });
            dataEventBus.Send(new DataEventModel<BasicLogMessage> { type = DataEventType.DELETE });

            mockClient.Verify(x => x.ReceiveAuditLog(It.IsAny<AuditLogMessage>()), Times.Never);
            mockClient.Verify(x => x.ReceiveLauncherLog(It.IsAny<LauncherLogMessage>()), Times.Never);
            mockClient.Verify(x => x.ReceiveErrorLog(It.IsAny<WebLogMessage>()), Times.Never);
            mockClient.Verify(x => x.ReceiveLog(It.IsAny<BasicLogMessage>()), Times.Never);
        }

        [Fact]
        public void ShouldRunAddedOnAddWithCorrectType() {
            Mock<IHubClients<IAdminClient>> mockHubClients = new Mock<IHubClients<IAdminClient>>();
            Mock<IAdminClient> mockClient = new Mock<IAdminClient>();

            mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.All).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveAuditLog(It.IsAny<AuditLogMessage>()));
            mockClient.Setup(x => x.ReceiveLauncherLog(It.IsAny<LauncherLogMessage>()));
            mockClient.Setup(x => x.ReceiveErrorLog(It.IsAny<WebLogMessage>()));
            mockClient.Setup(x => x.ReceiveLog(It.IsAny<BasicLogMessage>()));

            logEventHandler.Init();

            dataEventBus.Send(new DataEventModel<BasicLogMessage> { type = DataEventType.ADD, data = new AuditLogMessage() });
            dataEventBus.Send(new DataEventModel<BasicLogMessage> { type = DataEventType.ADD, data = new LauncherLogMessage("1.0.0", "test") });
            dataEventBus.Send(new DataEventModel<BasicLogMessage> { type = DataEventType.ADD, data = new WebLogMessage(new Exception("test")) });
            dataEventBus.Send(new DataEventModel<BasicLogMessage> { type = DataEventType.ADD, data = new BasicLogMessage("test") });

            mockClient.Verify(x => x.ReceiveAuditLog(It.IsAny<AuditLogMessage>()), Times.Once);
            mockClient.Verify(x => x.ReceiveLauncherLog(It.IsAny<LauncherLogMessage>()), Times.Once);
            mockClient.Verify(x => x.ReceiveErrorLog(It.IsAny<WebLogMessage>()), Times.Once);
            mockClient.Verify(x => x.ReceiveLog(It.IsAny<BasicLogMessage>()), Times.Once);
        }
    }
}
