using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.EventHandlers;
using UKSF.Api.Signalr.Clients;
using UKSF.Api.Signalr.Hubs;
using Xunit;

namespace UKSF.Api.Tests.Events.Handlers;

public class CommandRequestEventHandlerTests
{
    private readonly CommandRequestEventHandler _commandRequestEventHandler;
    private readonly IEventBus _eventBus;
    private readonly Mock<ICommandRequestsClient> _mockClient = new();

    public CommandRequestEventHandlerTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IUksfLogger> mockLoggingService = new();
        Mock<IHubContext<CommandRequestsHub, ICommandRequestsClient>> mockHub = new();
        Mock<IHubClients<ICommandRequestsClient>> mockHubClients = new();
        _eventBus = new EventBus();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainCommandRequest>(It.IsAny<string>()));

        mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
        mockHubClients.Setup(x => x.All).Returns(_mockClient.Object);

        _commandRequestEventHandler = new CommandRequestEventHandler(_eventBus, mockHub.Object, mockLoggingService.Object);
    }

    [Fact]
    public void ShouldNotRunEventOnDelete()
    {
        _commandRequestEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Delete, new ContextEventData<DomainCommandRequest>(null, null), ""));

        _mockClient.Verify(x => x.ReceiveRequestUpdate(), Times.Never);
    }

    [Fact]
    public void ShouldRunEventOnUpdateAndAdd()
    {
        _commandRequestEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Add, new ContextEventData<DomainCommandRequest>(null, null), ""));
        _eventBus.Send(new EventModel(EventType.Update, new ContextEventData<DomainCommandRequest>(null, null), ""));

        _mockClient.Verify(x => x.ReceiveRequestUpdate(), Times.Exactly(2));
    }
}
