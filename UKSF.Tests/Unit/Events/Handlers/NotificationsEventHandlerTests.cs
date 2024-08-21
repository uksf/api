using System;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Signalr.Clients;
using UKSF.Api.Core.Signalr.Hubs;
using UKSF.Api.EventHandlers;
using Xunit;

namespace UKSF.Tests.Unit.Events.Handlers;

public class NotificationsEventHandlerTests
{
    private readonly IEventBus _eventBus;
    private readonly Mock<IHubContext<NotificationHub, INotificationsClient>> _mockHub;
    private readonly Mock<IUksfLogger> _mockLoggingService;
    private readonly NotificationsEventHandler _notificationsEventHandler;

    public NotificationsEventHandlerTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        _mockLoggingService = new Mock<IUksfLogger>();
        _mockHub = new Mock<IHubContext<NotificationHub, INotificationsClient>>();

        _eventBus = new EventBus();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainNotification>(It.IsAny<string>()));

        _notificationsEventHandler = new NotificationsEventHandler(_eventBus, _mockHub.Object, _mockLoggingService.Object);
    }

    [Fact]
    public void ShouldLogOnException()
    {
        Mock<IHubClients<INotificationsClient>> mockHubClients = new();
        Mock<INotificationsClient> mockClient = new();

        _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
        mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
        mockClient.Setup(x => x.ReceiveNotification(It.IsAny<DomainNotification>())).Throws(new Exception());
        _mockLoggingService.Setup(x => x.LogError(It.IsAny<Exception>()));

        _notificationsEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Add, new ContextEventData<DomainNotification>(string.Empty, null), ""));

        _mockLoggingService.Verify(x => x.LogError(It.IsAny<Exception>()), Times.Once);
    }

    [Fact]
    public void ShouldNotRunEventOnUpdateOrDelete()
    {
        Mock<IHubClients<INotificationsClient>> mockHubClients = new();
        Mock<INotificationsClient> mockClient = new();

        _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
        mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
        mockClient.Setup(x => x.ReceiveNotification(It.IsAny<DomainNotification>()));

        _notificationsEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Update, new ContextEventData<DomainNotification>(string.Empty, null), ""));
        _eventBus.Send(new EventModel(EventType.Delete, new ContextEventData<DomainNotification>(string.Empty, null), ""));

        mockClient.Verify(x => x.ReceiveNotification(It.IsAny<DomainNotification>()), Times.Never);
    }

    [Fact]
    public void ShouldRunAddedOnAdd()
    {
        Mock<IHubClients<INotificationsClient>> mockHubClients = new();
        Mock<INotificationsClient> mockClient = new();

        _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
        mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
        mockClient.Setup(x => x.ReceiveNotification(It.IsAny<DomainNotification>()));

        _notificationsEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Add, new ContextEventData<DomainNotification>(string.Empty, new DomainNotification()), ""));

        mockClient.Verify(x => x.ReceiveNotification(It.IsAny<DomainNotification>()), Times.Once);
    }

    [Fact]
    public void ShouldUseOwnerAsIdInAdded()
    {
        Mock<IHubClients<INotificationsClient>> mockHubClients = new();
        Mock<INotificationsClient> mockClient = new();

        var subject = "";
        _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
        mockHubClients.Setup(x => x.Group("1")).Returns(mockClient.Object).Callback((string x) => subject = x);
        mockClient.Setup(x => x.ReceiveNotification(It.IsAny<DomainNotification>()));

        _notificationsEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Add, new ContextEventData<DomainNotification>(string.Empty, new DomainNotification { Owner = "1" }), ""));

        subject.Should().Be("1");
    }
}
