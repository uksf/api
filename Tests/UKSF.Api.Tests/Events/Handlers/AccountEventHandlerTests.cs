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
    private readonly Mock<IHubContext<AccountHub, IAccountClient>> _mockAccountHub;
    private readonly Mock<IUksfLogger> _mockLoggingService;

    public AccountEventHandlerTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IHubContext<AccountGroupedHub, IAccountGroupedClient>> mockGroupedHub = new();
        Mock<IHubContext<AllHub, IAllClient>> mockAllHub = new();
        _mockLoggingService = new Mock<IUksfLogger>();
        _mockAccountHub = new Mock<IHubContext<AccountHub, IAccountClient>>();
        _eventBus = new EventBus();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainAccount>(It.IsAny<string>()));
        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainUnit>(It.IsAny<string>()));

        _accountDataEventHandler = new AccountDataEventHandler(
            _eventBus,
            _mockAccountHub.Object,
            mockGroupedHub.Object,
            mockAllHub.Object,
            _mockLoggingService.Object
        );
    }

    [Fact]
    public void ShouldLogOnException()
    {
        Mock<IHubClients<IAccountClient>> mockHubClients = new();
        Mock<IAccountClient> mockAccountClient = new();

        _mockAccountHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
        mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockAccountClient.Object);
        mockAccountClient.Setup(x => x.ReceiveAccountUpdate()).Throws(new Exception());
        _mockLoggingService.Setup(x => x.LogError(It.IsAny<Exception>()));

        _accountDataEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Update, new ContextEventData<DomainAccount>(null, null), ""));
        _eventBus.Send(new EventModel(EventType.Update, new ContextEventData<DomainUnit>(null, null), ""));

        _mockLoggingService.Verify(x => x.LogError(It.IsAny<Exception>()), Times.Exactly(2));
    }

    [Fact]
    public void ShouldNotRunEvent()
    {
        Mock<IHubClients<IAccountClient>> mockHubClients = new();
        Mock<IAccountClient> mockAccountClient = new();

        _mockAccountHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
        mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockAccountClient.Object);
        mockAccountClient.Setup(x => x.ReceiveAccountUpdate());

        _accountDataEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Add, new ContextEventData<DomainAccount>(null, null), ""));
        _eventBus.Send(new EventModel(EventType.Delete, new ContextEventData<DomainAccount>(null, null), ""));
        _eventBus.Send(new EventModel(EventType.Add, new ContextEventData<DomainUnit>(null, null), ""));
        _eventBus.Send(new EventModel(EventType.Delete, new ContextEventData<DomainUnit>(null, null), ""));

        mockAccountClient.Verify(x => x.ReceiveAccountUpdate(), Times.Never);
    }

    [Fact]
    public void ShouldRunEventOnUpdate()
    {
        Mock<IHubClients<IAccountClient>> mockHubClients = new();
        Mock<IAccountClient> mockAccountClient = new();

        _mockAccountHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
        mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockAccountClient.Object);
        mockAccountClient.Setup(x => x.ReceiveAccountUpdate());

        _accountDataEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Update, new ContextEventData<DomainAccount>("1", null), ""));
        _eventBus.Send(new EventModel(EventType.Update, new ContextEventData<DomainUnit>("2", null), ""));

        mockAccountClient.Verify(x => x.ReceiveAccountUpdate(), Times.Exactly(2));
    }
}
