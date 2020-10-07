using System;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Events.Data;
using UKSF.Api.Events.Handlers;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Models.Command;
using UKSF.Api.Models.Events;
using UKSF.Api.Signalr.Hubs.Command;
using Xunit;

namespace UKSF.Tests.Unit.Events.Handlers {
    public class CommandRequestEventHandlerTests {
        private readonly CommandRequestEventHandler commandRequestEventHandler;
        private readonly DataEventBus<CommandRequest> dataEventBus;
        private readonly Mock<IHubContext<CommandRequestsHub, ICommandRequestsClient>> mockHub;
        private readonly Mock<ILoggingService> mockLoggingService;

        public CommandRequestEventHandlerTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            mockLoggingService = new Mock<ILoggingService>();
            mockHub = new Mock<IHubContext<CommandRequestsHub, ICommandRequestsClient>>();

            dataEventBus = new DataEventBus<CommandRequest>();

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<CommandRequest>(It.IsAny<string>()));

            commandRequestEventHandler = new CommandRequestEventHandler(dataEventBus, mockHub.Object, mockLoggingService.Object);
        }

        [Fact]
        public void ShouldLogOnException() {
            mockLoggingService.Setup(x => x.Log(It.IsAny<Exception>()));

            commandRequestEventHandler.Init();

            dataEventBus.Send(new DataEventModel<CommandRequest> { type = (DataEventType) 5 });

            mockLoggingService.Verify(x => x.Log(It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void ShouldNotRunEventOnDelete() {
            Mock<IHubClients<ICommandRequestsClient>> mockHubClients = new Mock<IHubClients<ICommandRequestsClient>>();
            Mock<ICommandRequestsClient> mockClient = new Mock<ICommandRequestsClient>();

            mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.All).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveRequestUpdate());

            commandRequestEventHandler.Init();

            dataEventBus.Send(new DataEventModel<CommandRequest> { type = DataEventType.DELETE });

            mockClient.Verify(x => x.ReceiveRequestUpdate(), Times.Never);
        }

        [Fact]
        public void ShouldRunEventOnUpdateAndAdd() {
            Mock<IHubClients<ICommandRequestsClient>> mockHubClients = new Mock<IHubClients<ICommandRequestsClient>>();
            Mock<ICommandRequestsClient> mockClient = new Mock<ICommandRequestsClient>();

            mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.All).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveRequestUpdate());

            commandRequestEventHandler.Init();

            dataEventBus.Send(new DataEventModel<CommandRequest> { type = DataEventType.ADD });
            dataEventBus.Send(new DataEventModel<CommandRequest> { type = DataEventType.UPDATE });

            mockClient.Verify(x => x.ReceiveRequestUpdate(), Times.Exactly(2));
        }
    }
}
