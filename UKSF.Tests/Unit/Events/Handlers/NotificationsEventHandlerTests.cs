using System;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.EventHandlers;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Signalr.Clients;
using UKSF.Api.Personnel.Signalr.Hubs;
using Xunit;

namespace UKSF.Tests.Unit.Events.Handlers {
    public class NotificationsEventHandlerTests {
        private readonly DataEventBus<Notification> dataEventBus;
        private readonly Mock<IHubContext<NotificationHub, INotificationsClient>> mockHub;
        private readonly Mock<ILogger> mockLoggingService;
        private readonly NotificationsEventHandler notificationsEventHandler;

        public NotificationsEventHandlerTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            mockLoggingService = new Mock<ILogger>();
            mockHub = new Mock<IHubContext<NotificationHub, INotificationsClient>>();

            dataEventBus = new DataEventBus<Notification>();

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<Notification>(It.IsAny<string>()));

            notificationsEventHandler = new NotificationsEventHandler(dataEventBus, mockHub.Object, mockLoggingService.Object);
        }

        [Fact]
        public void ShouldLogOnException() {
            Mock<IHubClients<INotificationsClient>> mockHubClients = new Mock<IHubClients<INotificationsClient>>();
            Mock<INotificationsClient> mockClient = new Mock<INotificationsClient>();

            mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveNotification(It.IsAny<Notification>())).Throws(new Exception());
            mockLoggingService.Setup(x => x.LogError(It.IsAny<Exception>()));

            notificationsEventHandler.Init();

            dataEventBus.Send(new DataEventModel<Notification> { type = DataEventType.ADD });

            mockLoggingService.Verify(x => x.LogError(It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void ShouldNotRunEventOnUpdateOrDelete() {
            Mock<IHubClients<INotificationsClient>> mockHubClients = new Mock<IHubClients<INotificationsClient>>();
            Mock<INotificationsClient> mockClient = new Mock<INotificationsClient>();

            mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveNotification(It.IsAny<Notification>()));

            notificationsEventHandler.Init();

            dataEventBus.Send(new DataEventModel<Notification> { type = DataEventType.UPDATE });
            dataEventBus.Send(new DataEventModel<Notification> { type = DataEventType.DELETE });

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

            dataEventBus.Send(new DataEventModel<Notification> { type = DataEventType.ADD, data = new Notification() });

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

            dataEventBus.Send(new DataEventModel<Notification> { type = DataEventType.ADD, data = new Notification { owner = "1" } });

            subject.Should().Be("1");
        }
    }
}
