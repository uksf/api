using System;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Events.Data;
using UKSF.Api.Events.Handlers;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Signalr.Hubs.Personnel;
using Xunit;

namespace UKSF.Tests.Unit.Events.Handlers {
    public class AccountEventHandlerTests {
        private readonly DataEventBus<Account> accountDataEventBus;
        private readonly AccountEventHandler accountEventHandler;
        private readonly Mock<IHubContext<AccountHub, IAccountClient>> mockAccountHub;
        private readonly Mock<ILoggingService> mockLoggingService;
        private readonly DataEventBus<Api.Models.Units.Unit> unitsDataEventBus;

        public AccountEventHandlerTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            mockLoggingService = new Mock<ILoggingService>();
            mockAccountHub = new Mock<IHubContext<AccountHub, IAccountClient>>();

            accountDataEventBus = new DataEventBus<Account>();
            unitsDataEventBus = new DataEventBus<Api.Models.Units.Unit>();

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<Account>(It.IsAny<string>()));
            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<Api.Models.Units.Unit>(It.IsAny<string>()));

            accountEventHandler = new AccountEventHandler(accountDataEventBus, unitsDataEventBus, mockAccountHub.Object, mockLoggingService.Object);
        }

        [Fact]
        public void ShouldLogOnException() {
            Mock<IHubClients<IAccountClient>> mockHubClients = new Mock<IHubClients<IAccountClient>>();
            Mock<IAccountClient> mockAccountClient = new Mock<IAccountClient>();

            mockAccountHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockAccountClient.Object);
            mockAccountClient.Setup(x => x.ReceiveAccountUpdate()).Throws(new Exception());
            mockLoggingService.Setup(x => x.Log(It.IsAny<Exception>()));

            accountEventHandler.Init();

            accountDataEventBus.Send(new DataEventModel<Account> { type = DataEventType.UPDATE });
            unitsDataEventBus.Send(new DataEventModel<Api.Models.Units.Unit> { type = DataEventType.UPDATE });

            mockLoggingService.Verify(x => x.Log(It.IsAny<Exception>()), Times.Exactly(2));
        }

        [Fact]
        public void ShouldNotRunEvent() {
            Mock<IHubClients<IAccountClient>> mockHubClients = new Mock<IHubClients<IAccountClient>>();
            Mock<IAccountClient> mockAccountClient = new Mock<IAccountClient>();

            mockAccountHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockAccountClient.Object);
            mockAccountClient.Setup(x => x.ReceiveAccountUpdate());

            accountEventHandler.Init();

            accountDataEventBus.Send(new DataEventModel<Account> { type = DataEventType.DELETE });
            unitsDataEventBus.Send(new DataEventModel<Api.Models.Units.Unit> { type = DataEventType.ADD });

            mockAccountClient.Verify(x => x.ReceiveAccountUpdate(), Times.Never);
        }

        [Fact]
        public void ShouldRunEventOnUpdate() {
            Mock<IHubClients<IAccountClient>> mockHubClients = new Mock<IHubClients<IAccountClient>>();
            Mock<IAccountClient> mockAccountClient = new Mock<IAccountClient>();

            mockAccountHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockAccountClient.Object);
            mockAccountClient.Setup(x => x.ReceiveAccountUpdate());

            accountEventHandler.Init();

            accountDataEventBus.Send(new DataEventModel<Account> { type = DataEventType.UPDATE });
            unitsDataEventBus.Send(new DataEventModel<Api.Models.Units.Unit> { type = DataEventType.UPDATE });

            mockAccountClient.Verify(x => x.ReceiveAccountUpdate(), Times.Exactly(2));
        }
    }
}
