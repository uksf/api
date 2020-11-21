using System;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Base.Context;
using UKSF.Api.Personnel.EventHandlers;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Personnel.Signalr.Clients;
using UKSF.Api.Personnel.Signalr.Hubs;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;
using Xunit;

namespace UKSF.Tests.Unit.Events.Handlers {
    public class CommentThreadEventHandlerTests {
        private readonly CommentThreadEventHandler _commentThreadEventHandler;
        private readonly DataEventBus<CommentThread> _dataEventBus;
        private readonly Mock<IHubContext<CommentThreadHub, ICommentThreadClient>> _mockHub;
        private readonly Mock<ILogger> _mockLoggingService;

        public CommentThreadEventHandlerTests() {
            Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
            Mock<ICommentThreadService> mockCommentThreadService = new();
            _mockLoggingService = new Mock<ILogger>();
            _mockHub = new Mock<IHubContext<CommentThreadHub, ICommentThreadClient>>();

            _dataEventBus = new DataEventBus<CommentThread>();

            mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<CommentThread>(It.IsAny<string>()));
            mockCommentThreadService.Setup(x => x.FormatComment(It.IsAny<Comment>())).Returns(null);

            _commentThreadEventHandler = new CommentThreadEventHandler(_dataEventBus, _mockHub.Object, mockCommentThreadService.Object, _mockLoggingService.Object);
        }

        [Fact]
        public void ShouldLogOnException() {
            _mockLoggingService.Setup(x => x.LogError(It.IsAny<Exception>()));

            _commentThreadEventHandler.Init();

            _dataEventBus.Send(new DataEventModel<CommentThread> { Type = (DataEventType) 5 });

            _mockLoggingService.Verify(x => x.LogError(It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public void ShouldNotRunEventOnUpdate() {
            Mock<IHubClients<ICommentThreadClient>> mockHubClients = new();
            Mock<ICommentThreadClient> mockClient = new();

            _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveComment(It.IsAny<object>()));
            mockClient.Setup(x => x.DeleteComment(It.IsAny<string>()));

            _commentThreadEventHandler.Init();

            _dataEventBus.Send(new DataEventModel<CommentThread> { Type = DataEventType.UPDATE, Data = new Comment() });

            mockClient.Verify(x => x.ReceiveComment(It.IsAny<object>()), Times.Never);
            mockClient.Verify(x => x.DeleteComment(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void ShouldRunAddedOnAdd() {
            Mock<IHubClients<ICommentThreadClient>> mockHubClients = new();
            Mock<ICommentThreadClient> mockClient = new();

            _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveComment(It.IsAny<object>()));
            mockClient.Setup(x => x.DeleteComment(It.IsAny<string>()));

            _commentThreadEventHandler.Init();

            _dataEventBus.Send(new DataEventModel<CommentThread> { Type = DataEventType.ADD, Data = new Comment() });

            mockClient.Verify(x => x.ReceiveComment(It.IsAny<object>()), Times.Once);
            mockClient.Verify(x => x.DeleteComment(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void ShouldRunDeletedOnDelete() {
            Mock<IHubClients<ICommentThreadClient>> mockHubClients = new();
            Mock<ICommentThreadClient> mockClient = new();

            _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveComment(It.IsAny<object>()));
            mockClient.Setup(x => x.DeleteComment(It.IsAny<string>()));

            _commentThreadEventHandler.Init();

            _dataEventBus.Send(new DataEventModel<CommentThread> { Type = DataEventType.DELETE, Data = new Comment() });

            mockClient.Verify(x => x.ReceiveComment(It.IsAny<object>()), Times.Never);
            mockClient.Verify(x => x.DeleteComment(It.IsAny<string>()), Times.Once);
        }
    }
}
