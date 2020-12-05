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
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;
using Xunit;

namespace UKSF.Tests.Unit.Events.Handlers {
    public class NotificationsEventHandlerTests {
        private readonly IEventBus _eventBus;
        private readonly Mock<IHubContext<NotificationHub, INotificationsClient>> _mockHub;
        private readonly Mock<ILogger> _mockLoggingService;
        private readonly NotificationsEventHandler _notificationsEventHandler;

        public NotificationsEventHandlerTests() {
            Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
            _mockLoggingService = new Mock<ILogger>();
            _mockHub = new Mock<IHubContext<NotificationHub, INotificationsClient>>();

            _eventBus = new EventBus();

            mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<Notification>(It.IsAny<string>()));

            _notificationsEventHandler = new NotificationsEventHandler(_eventBus, _mockHub.Object, _mockLoggingService.Object);
        }

        [Fact]
        public void ShouldLogOnException() {
            Mock<IHubClients<INotificationsClient>> mockHubClients = new();
            Mock<INotificationsClient> mockClient = new();

            _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveNotification(It.IsAny<Notification>())).Throws(new Exception());
            _mockLoggingService.Setup(x => x.LogError(It.IsAny<Exception>()));

            _notificationsEventHandler.Init();

            _eventBus.Send(new EventModel(EventType.ADD, new ContextEventData<Notification>(string.Empty, null)));

            _mockLoggingService.Verify(x => x.LogError(It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void ShouldNotRunEventOnUpdateOrDelete() {
            Mock<IHubClients<INotificationsClient>> mockHubClients = new();
            Mock<INotificationsClient> mockClient = new();

            _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveNotification(It.IsAny<Notification>()));

            _notificationsEventHandler.Init();

            _eventBus.Send(new EventModel(EventType.UPDATE, new ContextEventData<Notification>(string.Empty, null)));
            _eventBus.Send(new EventModel(EventType.DELETE, new ContextEventData<Notification>(string.Empty, null)));

            mockClient.Verify(x => x.ReceiveNotification(It.IsAny<Notification>()), Times.Never);
        }

        [Fact]
        public void ShouldRunAddedOnAdd() {
            Mock<IHubClients<INotificationsClient>> mockHubClients = new();
            Mock<INotificationsClient> mockClient = new();

            _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveNotification(It.IsAny<Notification>()));

            _notificationsEventHandler.Init();

            _eventBus.Send(new EventModel(EventType.ADD, new ContextEventData<Notification>(string.Empty, new Notification())));

            mockClient.Verify(x => x.ReceiveNotification(It.IsAny<Notification>()), Times.Once);
        }

        [Fact]
        public void ShouldUseOwnerAsIdInAdded() {
            Mock<IHubClients<INotificationsClient>> mockHubClients = new();
            Mock<INotificationsClient> mockClient = new();

            string subject = "";
            _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group("1")).Returns(mockClient.Object).Callback((string x) => subject = x);
            mockClient.Setup(x => x.ReceiveNotification(It.IsAny<Notification>()));

            _notificationsEventHandler.Init();

            _eventBus.Send(new EventModel(EventType.ADD, new ContextEventData<Notification>(string.Empty, new Notification() { Owner = "1" })));

            subject.Should().Be("1");
        }
    }
}
