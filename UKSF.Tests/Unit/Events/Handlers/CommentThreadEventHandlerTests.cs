using System;
using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Data.Message;
using UKSF.Api.Events.Data;
using UKSF.Api.Events.Handlers;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Hubs;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Models.Events;
using UKSF.Api.Models.Message;
using UKSF.Api.Signalr.Hubs.Message;
using Xunit;

namespace UKSF.Tests.Unit.Unit.Events.Handlers {
    public class CommentThreadEventHandlerTests {
        private readonly CommentThreadEventHandler commentThreadEventHandler;
        private readonly DataEventBus<ICommentThreadDataService> dataEventBus;
        private readonly Mock<IHubContext<CommentThreadHub, ICommentThreadClient>> mockHub;
        private readonly Mock<ILoggingService> mockLoggingService;

        public CommentThreadEventHandlerTests() {
            Mock<IDataCollectionFactory> mockDataCollectionFactory = new Mock<IDataCollectionFactory>();
            Mock<ICommentThreadService> mockCommentThreadService = new Mock<ICommentThreadService>();
            mockLoggingService = new Mock<ILoggingService>();
            mockHub = new Mock<IHubContext<CommentThreadHub, ICommentThreadClient>>();

            dataEventBus = new DataEventBus<ICommentThreadDataService>();
            ICommentThreadDataService dataService = new CommentThreadDataService(mockDataCollectionFactory.Object, dataEventBus);

            mockDataCollectionFactory.Setup(x => x.CreateDataCollection<CommentThread>(It.IsAny<string>()));
            mockCommentThreadService.Setup(x => x.FormatComment(It.IsAny<Comment>())).Returns(null);

            commentThreadEventHandler = new CommentThreadEventHandler(dataService, mockHub.Object, mockCommentThreadService.Object, mockLoggingService.Object);
        }

        [Fact]
        public void ShouldNotRunEventOnUpdate() {
            Mock<IHubClients<ICommentThreadClient>> mockHubClients = new Mock<IHubClients<ICommentThreadClient>>();
            Mock<ICommentThreadClient> mockClient = new Mock<ICommentThreadClient>();

            mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveComment(It.IsAny<object>()));
            mockClient.Setup(x => x.DeleteComment(It.IsAny<string>()));

            commentThreadEventHandler.Init();

            dataEventBus.Send(new DataEventModel<ICommentThreadDataService> { type = DataEventType.UPDATE, data = new Comment() });

            mockClient.Verify(x => x.ReceiveComment(It.IsAny<object>()), Times.Never);
            mockClient.Verify(x => x.DeleteComment(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void ShouldRunAddedOnAdd() {
            Mock<IHubClients<ICommentThreadClient>> mockHubClients = new Mock<IHubClients<ICommentThreadClient>>();
            Mock<ICommentThreadClient> mockClient = new Mock<ICommentThreadClient>();

            mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveComment(It.IsAny<object>()));
            mockClient.Setup(x => x.DeleteComment(It.IsAny<string>()));

            commentThreadEventHandler.Init();

            dataEventBus.Send(new DataEventModel<ICommentThreadDataService> { type = DataEventType.ADD, data = new Comment() });

            mockClient.Verify(x => x.ReceiveComment(It.IsAny<object>()), Times.Once);
            mockClient.Verify(x => x.DeleteComment(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void ShouldRunDeletedOnDelete() {
            Mock<IHubClients<ICommentThreadClient>> mockHubClients = new Mock<IHubClients<ICommentThreadClient>>();
            Mock<ICommentThreadClient> mockClient = new Mock<ICommentThreadClient>();

            mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
            mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
            mockClient.Setup(x => x.ReceiveComment(It.IsAny<object>()));
            mockClient.Setup(x => x.DeleteComment(It.IsAny<string>()));

            commentThreadEventHandler.Init();

            dataEventBus.Send(new DataEventModel<ICommentThreadDataService> { type = DataEventType.DELETE, data = new Comment() });

            mockClient.Verify(x => x.ReceiveComment(It.IsAny<object>()), Times.Never);
            mockClient.Verify(x => x.DeleteComment(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void ShouldLogOnException() {
            mockLoggingService.Setup(x => x.Log(It.IsAny<Exception>()));

            commentThreadEventHandler.Init();

            dataEventBus.Send(new DataEventModel<ICommentThreadDataService> { type = (DataEventType) 5 });

            mockLoggingService.Verify(x => x.Log(It.IsAny<Exception>()), Times.Once);
        }
    }
}
