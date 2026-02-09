using System;
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

public class AccountEventHandlerTests
{
    private readonly AccountDataEventHandler _accountDataEventHandler;
    private readonly IEventBus _eventBus;
    private readonly Mock<IAccountClient> _mockAccountClient = new();
    private readonly Mock<IUksfLogger> _mockLoggingService = new();

    public AccountEventHandlerTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IHubContext<AccountGroupedHub, IAccountGroupedClient>> mockGroupedHub = new();
        Mock<IHubContext<AllHub, IAllClient>> mockAllHub = new();
        Mock<IHubContext<AccountHub, IAccountClient>> mockAccountHub = new();
        Mock<IHubClients<IAccountClient>> mockHubClients = new();
        _eventBus = new EventBus();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainAccount>(It.IsAny<string>()));
        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainUnit>(It.IsAny<string>()));

        mockAccountHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
        mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(_mockAccountClient.Object);

        _accountDataEventHandler = new AccountDataEventHandler(
            _eventBus,
            mockAccountHub.Object,
            mockGroupedHub.Object,
            mockAllHub.Object,
            _mockLoggingService.Object
        );
    }

    [Fact]
    public void ShouldLogOnException()
    {
        _mockAccountClient.Setup(x => x.ReceiveAccountUpdate()).Throws(new Exception());

        _accountDataEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Update, new ContextEventData<DomainAccount>(null, null), ""));
        _eventBus.Send(new EventModel(EventType.Update, new ContextEventData<DomainUnit>(null, null), ""));

        _mockLoggingService.Verify(x => x.LogError(It.IsAny<Exception>()), Times.Exactly(2));
    }

    [Fact]
    public void ShouldNotRunEvent()
    {
        _accountDataEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Add, new ContextEventData<DomainAccount>(null, null), ""));
        _eventBus.Send(new EventModel(EventType.Delete, new ContextEventData<DomainAccount>(null, null), ""));
        _eventBus.Send(new EventModel(EventType.Add, new ContextEventData<DomainUnit>(null, null), ""));
        _eventBus.Send(new EventModel(EventType.Delete, new ContextEventData<DomainUnit>(null, null), ""));

        _mockAccountClient.Verify(x => x.ReceiveAccountUpdate(), Times.Never);
    }

    [Fact]
    public void ShouldRunEventOnUpdate()
    {
        _accountDataEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Update, new ContextEventData<DomainAccount>("1", null), ""));
        _eventBus.Send(new EventModel(EventType.Update, new ContextEventData<DomainUnit>("2", null), ""));

        _mockAccountClient.Verify(x => x.ReceiveAccountUpdate(), Times.Exactly(2));
    }
}
