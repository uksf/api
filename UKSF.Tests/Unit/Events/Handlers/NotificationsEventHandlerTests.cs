using System;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Data.Message;
using UKSF.Api.Events.Data;
using UKSF.Api.Events.Handlers;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Message;
using UKSF.Api.Signalr.Hubs.Message;
using Xunit;

namespace UKSF.Tests.Unit.Unit.Events.Handlers {
    public class NotificationsEventHandlerTests {
        private readonly DataEventBus<INotificationsDataService> dataEventBus;
        private readonly Mock<IHubContext<NotificationHub, INotificationsClient>> mockHub;
        private readonly NotificationsEventHandler notificationsEventHandler;
        private Mock<ILoggingService> mockLoggingService;

        public NotificationsEventHandlerTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            mockLoggingService = new Mock<ILoggingService>();
            mockHub = new Mock<IHubContext<NotificationHub, INotificationsClient>>();

            dataEventBus = new DataEventBus<INotificationsDataService>();
            INotificationsDataService dataService = new NotificationsDataService(mockDataCollectionFactory.Object, dataEventBus);

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<Notification>(It.IsAny<string>()));

            notificationsEventHandler = new NotificationsEventHandler(dataService, mockHub.Object, mockLoggingService.Object);
        }

        [Fact]
        public void ShouldNotRunEventOnUpdateOrDelete() {
            Mock<IHubClients<INotificationsClient>> mockHubClients = new Mock<IHubClients<INotificationsClient>>();
            Mock<INotificationsClient> mockClient = new Mock<INotificationsClient>();

            mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveNotification(It.IsAny<Notification>()));

            notificationsEventHandler.Init();

            dataEventBus.Send(new DataEventModel<INotificationsDataService> { type = DataEventType.UPDATE });
            dataEventBus.Send(new DataEventModel<INotificationsDataService> { type = DataEventType.DELETE });

            mockClient.Verify(x => x.ReceiveNotification(It.IsAny<Notification>()), Times.Never);
        }

        [Fact]
        public void ShouldRunAddedOnAdd() {
            Mock<IHubClients<INotificationsClient>> mockHubClients = new Mock<IHubClients<INotificationsClient>>();
            Mock<INotificationsClient> mockClient = new Mock<INotificationsClient>();

            mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveNotification(It.IsAny<Notification>()));

            notificationsEventHandler.Init();

            dataEventBus.Send(new DataEventModel<INotificationsDataService> { type = DataEventType.ADD, data = new Notification() });

            mockClient.Verify(x => x.ReceiveNotification(It.IsAny<Notification>()), Times.Once);
        }

        [Fact]
        public void ShouldUseOwnerAsIdInAdded() {
            Mock<IHubClients<INotificationsClient>> mockHubClients = new Mock<IHubClients<INotificationsClient>>();
            Mock<INotificationsClient> mockClient = new Mock<INotificationsClient>();

            string subject = "";
            mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group("1")).Returns(mockClient.Object).Callback((string x) => subject = x);
            mockClient.Setup(x => x.ReceiveNotification(It.IsAny<Notification>()));

            notificationsEventHandler.Init();

            dataEventBus.Send(new DataEventModel<INotificationsDataService> { type = DataEventType.ADD, data = new Notification { owner = "1" } });

            subject.Should().Be("1");
        }

        [Fact]
        public void ShouldLogOnException() {
            Mock<IHubClients<INotificationsClient>> mockHubClients = new Mock<IHubClients<INotificationsClient>>();
            Mock<INotificationsClient> mockClient = new Mock<INotificationsClient>();

            mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveNotification(It.IsAny<Notification>())).Throws(new Exception());
            mockLoggingService.Setup(x => x.Log(It.IsAny<Exception>()));

            notificationsEventHandler.Init();

            dataEventBus.Send(new DataEventModel<INotificationsDataService> { type = DataEventType.ADD });

            mockLoggingService.Verify(x => x.Log(It.IsAny<Exception>()), Times.Once);
        }
    }
}
