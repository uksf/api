using System;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Data.Command;
using UKSF.Api.Events.Data;
using UKSF.Api.Events.Handlers;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Models.Command;
using UKSF.Api.Models.Events;
using UKSF.Api.Signalr.Hubs.Command;
using Xunit;

namespace UKSF.Tests.Unit.Unit.Events.Handlers {
    public class CommandRequestEventHandlerTests {
        private readonly CommandRequestEventHandler commandRequestEventHandler;
        private readonly DataEventBus<ICommandRequestDataService> dataEventBus;
        private readonly Mock<IHubContext<CommandRequestsHub, ICommandRequestsClient>> mockHub;
        private Mock<ILoggingService> mockLoggingService;

        public CommandRequestEventHandlerTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            mockLoggingService = new Mock<ILoggingService>();
            mockHub = new Mock<IHubContext<CommandRequestsHub, ICommandRequestsClient>>();

            dataEventBus = new DataEventBus<ICommandRequestDataService>();
            ICommandRequestDataService dataService = new CommandRequestDataService(mockDataCollectionFactory.Object, dataEventBus);

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<CommandRequest>(It.IsAny<string>()));

            commandRequestEventHandler = new CommandRequestEventHandler(dataService, mockHub.Object, mockLoggingService.Object);
        }

        [Fact]
        public void ShouldNotRunEventOnDelete() {
            Mock<IHubClients<ICommandRequestsClient>> mockHubClients = new Mock<IHubClients<ICommandRequestsClient>>();
            Mock<ICommandRequestsClient> mockClient = new Mock<ICommandRequestsClient>();

            mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.All).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveRequestUpdate());

            commandRequestEventHandler.Init();

            dataEventBus.Send(new DataEventModel<ICommandRequestDataService> { type = DataEventType.DELETE });

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

            dataEventBus.Send(new DataEventModel<ICommandRequestDataService> { type = DataEventType.ADD });
            dataEventBus.Send(new DataEventModel<ICommandRequestDataService> { type = DataEventType.UPDATE });

            mockClient.Verify(x => x.ReceiveRequestUpdate(), Times.Exactly(2));
        }

        [Fact]
        public void ShouldLogOnException() {
            Mock<IHubClients<ICommandRequestsClient>> mockHubClients = new Mock<IHubClients<ICommandRequestsClient>>();
            Mock<ICommandRequestsClient> mockClient = new Mock<ICommandRequestsClient>();

            mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.All).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveRequestUpdate()).Throws(new Exception());
            mockLoggingService.Setup(x => x.Log(It.IsAny<Exception>()));

            commandRequestEventHandler.Init();

            dataEventBus.Send(new DataEventModel<ICommandRequestDataService> { type = DataEventType.UPDATE });

            mockLoggingService.Verify(x => x.Log(It.IsAny<Exception>()), Times.Once);
        }
    }
}
