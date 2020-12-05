using System;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.EventHandlers;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Signalr.Clients;
using UKSF.Api.Personnel.Signalr.Hubs;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;
using Xunit;

namespace UKSF.Tests.Unit.Events.Handlers {
    public class AccountEventHandlerTests {
        private readonly IEventBus _eventBus;
        private readonly AccountDataEventHandler _accountDataEventHandler;
        private readonly Mock<IHubContext<AccountHub, IAccountClient>> _mockAccountHub;
        private readonly Mock<ILogger> _mockLoggingService;

        public AccountEventHandlerTests() {
            Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
            _mockLoggingService = new Mock<ILogger>();
            _mockAccountHub = new Mock<IHubContext<AccountHub, IAccountClient>>();
            _eventBus = new EventBus();

            mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<Account>(It.IsAny<string>()));
            mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<Api.Personnel.Models.Unit>(It.IsAny<string>()));

            _accountDataEventHandler = new AccountDataEventHandler(_eventBus, _mockAccountHub.Object, _mockLoggingService.Object);
        }

        [Fact]
        public void ShouldLogOnException() {
            Mock<IHubClients<IAccountClient>> mockHubClients = new();
            Mock<IAccountClient> mockAccountClient = new();

            _mockAccountHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockAccountClient.Object);
            mockAccountClient.Setup(x => x.ReceiveAccountUpdate()).Throws(new Exception());
            _mockLoggingService.Setup(x => x.LogError(It.IsAny<Exception>()));

            _accountDataEventHandler.Init();

            _eventBus.Send(new EventModel(EventType.UPDATE, new ContextEventData<Account>(null, null)));
            _eventBus.Send(new EventModel(EventType.UPDATE, new ContextEventData<Api.Personnel.Models.Unit>(null, null)));

            _mockLoggingService.Verify(x => x.LogError(It.IsAny<Exception>()), Times.Exactly(2));
        }

        [Fact]
        public void ShouldNotRunEvent() {
            Mock<IHubClients<IAccountClient>> mockHubClients = new();
            Mock<IAccountClient> mockAccountClient = new();

            _mockAccountHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockAccountClient.Object);
            mockAccountClient.Setup(x => x.ReceiveAccountUpdate());

            _accountDataEventHandler.Init();

            _eventBus.Send(new EventModel(EventType.ADD, new ContextEventData<Account>(null, null)));
            _eventBus.Send(new EventModel(EventType.DELETE, new ContextEventData<Account>(null, null)));
            _eventBus.Send(new EventModel(EventType.ADD, new ContextEventData<Api.Personnel.Models.Unit>(null, null)));
            _eventBus.Send(new EventModel(EventType.DELETE, new ContextEventData<Api.Personnel.Models.Unit>(null, null)));

            mockAccountClient.Verify(x => x.ReceiveAccountUpdate(), Times.Never);
        }

        [Fact]
        public void ShouldRunEventOnUpdate() {
            Mock<IHubClients<IAccountClient>> mockHubClients = new();
            Mock<IAccountClient> mockAccountClient = new();

            _mockAccountHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockAccountClient.Object);
            mockAccountClient.Setup(x => x.ReceiveAccountUpdate());

            _accountDataEventHandler.Init();

            _eventBus.Send(new EventModel(EventType.UPDATE, new ContextEventData<Account>("1", null)));
            _eventBus.Send(new EventModel(EventType.UPDATE, new ContextEventData<Api.Personnel.Models.Unit>("2", null)));

            mockAccountClient.Verify(x => x.ReceiveAccountUpdate(), Times.Exactly(2));
        }
    }
}
