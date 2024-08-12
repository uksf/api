using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.EventHandlers;
using UKSF.Api.Signalr.Clients;
using UKSF.Api.Signalr.Hubs;
using Xunit;

namespace UKSF.Tests.Unit.Events.Handlers;

public class CommandRequestEventHandlerTests
{
    private readonly CommandRequestEventHandler _commandRequestEventHandler;
    private readonly IEventBus _eventBus;
    private readonly Mock<IHubContext<CommandRequestsHub, ICommandRequestsClient>> _mockHub;

    public CommandRequestEventHandlerTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IUksfLogger> mockLoggingService = new();
        _mockHub = new Mock<IHubContext<CommandRequestsHub, ICommandRequestsClient>>();
        _eventBus = new EventBus();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<CommandRequest>(It.IsAny<string>()));

        _commandRequestEventHandler = new CommandRequestEventHandler(_eventBus, _mockHub.Object, mockLoggingService.Object);
    }

    [Fact]
    public void ShouldNotRunEventOnDelete()
    {
        Mock<IHubClients<ICommandRequestsClient>> mockHubClients = new();
        Mock<ICommandRequestsClient> mockClient = new();

        _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
        mockHubClients.Setup(x => x.All).Returns(mockClient.Object);
        mockClient.Setup(x => x.ReceiveRequestUpdate());

        _commandRequestEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Delete, new ContextEventData<CommandRequest>(null, null), ""));

        mockClient.Verify(x => x.ReceiveRequestUpdate(), Times.Never);
    }

    [Fact]
    public void ShouldRunEventOnUpdateAndAdd()
    {
        Mock<IHubClients<ICommandRequestsClient>> mockHubClients = new();
        Mock<ICommandRequestsClient> mockClient = new();

        _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
        mockHubClients.Setup(x => x.All).Returns(mockClient.Object);
        mockClient.Setup(x => x.ReceiveRequestUpdate());

        _commandRequestEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Add, new ContextEventData<CommandRequest>(null, null), ""));
        _eventBus.Send(new EventModel(EventType.Update, new ContextEventData<CommandRequest>(null, null), ""));

        mockClient.Verify(x => x.ReceiveRequestUpdate(), Times.Exactly(2));
    }
}
