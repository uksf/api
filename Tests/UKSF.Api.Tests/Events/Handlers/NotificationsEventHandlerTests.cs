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

namespace UKSF.Api.Tests.Events.Handlers;

public class NotificationsEventHandlerTests
{
    private readonly IEventBus _eventBus;
    private readonly Mock<INotificationsClient> _mockClient = new();
    private readonly Mock<IHubClients<INotificationsClient>> _mockHubClients = new();
    private readonly Mock<IUksfLogger> _mockLoggingService = new();
    private readonly NotificationsEventHandler _notificationsEventHandler;

    public NotificationsEventHandlerTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IHubContext<NotificationHub, INotificationsClient>> mockHub = new();
        _eventBus = new EventBus();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainNotification>(It.IsAny<string>()));

        mockHub.Setup(x => x.Clients).Returns(_mockHubClients.Object);
        _mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(_mockClient.Object);

        _notificationsEventHandler = new NotificationsEventHandler(_eventBus, mockHub.Object, _mockLoggingService.Object);
    }

    [Fact]
    public void ShouldLogOnException()
    {
        _mockClient.Setup(x => x.ReceiveNotification(It.IsAny<DomainNotification>())).Throws(new Exception());

        _notificationsEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Add, new ContextEventData<DomainNotification>(string.Empty, null), ""));

        _mockLoggingService.Verify(x => x.LogError(It.IsAny<Exception>()), Times.Once);
    }

    [Fact]
    public void ShouldNotRunEventOnUpdateOrDelete()
    {
        _notificationsEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Update, new ContextEventData<DomainNotification>(string.Empty, null), ""));
        _eventBus.Send(new EventModel(EventType.Delete, new ContextEventData<DomainNotification>(string.Empty, null), ""));

        _mockClient.Verify(x => x.ReceiveNotification(It.IsAny<DomainNotification>()), Times.Never);
    }

    [Fact]
    public void ShouldRunAddedOnAdd()
    {
        _notificationsEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Add, new ContextEventData<DomainNotification>(string.Empty, new DomainNotification()), ""));

        _mockClient.Verify(x => x.ReceiveNotification(It.IsAny<DomainNotification>()), Times.Once);
    }

    [Fact]
    public void ShouldUseOwnerAsIdInAdded()
    {
        var subject = "";
        _mockHubClients.Setup(x => x.Group("1")).Returns(_mockClient.Object).Callback((string x) => subject = x);

        _notificationsEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Add, new ContextEventData<DomainNotification>(string.Empty, new DomainNotification { Owner = "1" }), ""));

        subject.Should().Be("1");
    }
}
