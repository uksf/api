using System;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Base.Context;
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
        private readonly DataEventBus<CommandRequest> _dataEventBus;
        private readonly Mock<IHubContext<CommandRequestsHub, ICommandRequestsClient>> _mockHub;
        private readonly Mock<ILogger> _mockLoggingService;

        public CommandRequestEventHandlerTests() {
            Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
            _mockLoggingService = new Mock<ILogger>();
            _mockHub = new Mock<IHubContext<CommandRequestsHub, ICommandRequestsClient>>();

            _dataEventBus = new DataEventBus<CommandRequest>();

            mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<CommandRequest>(It.IsAny<string>()));

            _commandRequestEventHandler = new CommandRequestEventHandler(_dataEventBus, _mockHub.Object, _mockLoggingService.Object);
        }

        [Fact]
        public void ShouldLogOnException() {
            _mockLoggingService.Setup(x => x.LogError(It.IsAny<Exception>()));

            _commandRequestEventHandler.Init();

            _dataEventBus.Send(new DataEventModel<CommandRequest> { Type = (DataEventType) 5 });

            _mockLoggingService.Verify(x => x.LogError(It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void ShouldNotRunEventOnDelete() {
            Mock<IHubClients<ICommandRequestsClient>> mockHubClients = new();
            Mock<ICommandRequestsClient> mockClient = new();

            _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.All).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveRequestUpdate());

            _commandRequestEventHandler.Init();

            _dataEventBus.Send(new DataEventModel<CommandRequest> { Type = DataEventType.DELETE });

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

            _dataEventBus.Send(new DataEventModel<CommandRequest> { Type = DataEventType.ADD });
            _dataEventBus.Send(new DataEventModel<CommandRequest> { Type = DataEventType.UPDATE });

            mockClient.Verify(x => x.ReceiveRequestUpdate(), Times.Exactly(2));
        }
    }
}
