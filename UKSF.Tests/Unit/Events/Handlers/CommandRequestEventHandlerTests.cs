using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Command.EventHandlers;
using UKSF.Api.Command.Models;
using UKSF.Api.Command.Signalr.Clients;
using UKSF.Api.Command.Signalr.Hubs;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;
using Xunit;

namespace UKSF.Tests.Unit.Events.Handlers {
    public class CommandRequestEventHandlerTests {
        private readonly CommandRequestEventHandler _commandRequestEventHandler;
        private readonly IEventBus _eventBus;
        private readonly Mock<IHubContext<CommandRequestsHub, ICommandRequestsClient>> _mockHub;

        public CommandRequestEventHandlerTests() {
            Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
            Mock<ILogger> mockLoggingService = new();
            _mockHub = new Mock<IHubContext<CommandRequestsHub, ICommandRequestsClient>>();
            _eventBus = new EventBus();

            mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<CommandRequest>(It.IsAny<string>()));

            _commandRequestEventHandler = new CommandRequestEventHandler(_eventBus, _mockHub.Object, mockLoggingService.Object);
        }

        [Fact]
        public void ShouldNotRunEventOnDelete() {
            Mock<IHubClients<ICommandRequestsClient>> mockHubClients = new();
            Mock<ICommandRequestsClient> mockClient = new();

            _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.All).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveRequestUpdate());

            _commandRequestEventHandler.Init();

            _eventBus.Send(new EventModel(EventType.DELETE, new ContextEventData<CommandRequest>(null, null)));

            mockClient.Verify(x => x.ReceiveRequestUpdate(), Times.Never);
        }

        [Fact]
        public void ShouldRunEventOnUpdateAndAdd() {
            Mock<IHubClients<ICommandRequestsClient>> mockHubClients = new();
            Mock<ICommandRequestsClient> mockClient = new();

            _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.All).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveRequestUpdate());

            _commandRequestEventHandler.Init();

            _eventBus.Send(new EventModel(EventType.ADD, new ContextEventData<CommandRequest>(null, null)));
            _eventBus.Send(new EventModel(EventType.UPDATE, new ContextEventData<CommandRequest>(null, null)));

            mockClient.Verify(x => x.ReceiveRequestUpdate(), Times.Exactly(2));
        }
    }
}
