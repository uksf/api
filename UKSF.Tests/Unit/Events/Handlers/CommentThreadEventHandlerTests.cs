using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.EventHandlers;
using UKSF.Api.Services;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;
using UKSF.Api.Signalr.Clients;
using UKSF.Api.Signalr.Hubs;
using Xunit;

namespace UKSF.Tests.Unit.Events.Handlers;

public class CommentThreadEventHandlerTests
{
    private readonly CommentThreadEventHandler _commentThreadEventHandler;
    private readonly IEventBus _eventBus;
    private readonly Mock<IHubContext<CommentThreadHub, ICommentThreadClient>> _mockHub;

    public CommentThreadEventHandlerTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<ICommentThreadService> mockCommentThreadService = new();
        Mock<IUksfLogger> mockLoggingService = new();
        _mockHub = new();
        _eventBus = new EventBus();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<CommentThread>(It.IsAny<string>()));
        mockCommentThreadService.Setup(x => x.FormatComment(It.IsAny<Comment>())).Returns(null);

        _commentThreadEventHandler = new(_eventBus, _mockHub.Object, mockCommentThreadService.Object, mockLoggingService.Object);
    }

    [Fact]
    public void ShouldNotRunEventOnUpdate()
    {
        Mock<IHubClients<ICommentThreadClient>> mockHubClients = new();
        Mock<ICommentThreadClient> mockClient = new();

        _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
        mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
        mockClient.Setup(x => x.ReceiveComment(It.IsAny<object>()));
        mockClient.Setup(x => x.DeleteComment(It.IsAny<string>()));

        _commentThreadEventHandler.Init();

        _eventBus.Send(new(EventType.UPDATE, new CommentThreadEventData(string.Empty, new())));

        mockClient.Verify(x => x.ReceiveComment(It.IsAny<object>()), Times.Never);
        mockClient.Verify(x => x.DeleteComment(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ShouldRunAddedOnAdd()
    {
        Mock<IHubClients<ICommentThreadClient>> mockHubClients = new();
        Mock<ICommentThreadClient> mockClient = new();

        _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
        mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
        mockClient.Setup(x => x.ReceiveComment(It.IsAny<object>()));
        mockClient.Setup(x => x.DeleteComment(It.IsAny<string>()));

        _commentThreadEventHandler.Init();

        _eventBus.Send(new(EventType.ADD, new CommentThreadEventData(string.Empty, new())));

        mockClient.Verify(x => x.ReceiveComment(It.IsAny<object>()), Times.Once);
        mockClient.Verify(x => x.DeleteComment(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ShouldRunDeletedOnDelete()
    {
        Mock<IHubClients<ICommentThreadClient>> mockHubClients = new();
        Mock<ICommentThreadClient> mockClient = new();

        _mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
        mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(mockClient.Object);
        mockClient.Setup(x => x.ReceiveComment(It.IsAny<object>()));
        mockClient.Setup(x => x.DeleteComment(It.IsAny<string>()));

        _commentThreadEventHandler.Init();

        _eventBus.Send(new(EventType.DELETE, new CommentThreadEventData(string.Empty, new())));

        mockClient.Verify(x => x.ReceiveComment(It.IsAny<object>()), Times.Never);
        mockClient.Verify(x => x.DeleteComment(It.IsAny<string>()), Times.Once);
    }
}
