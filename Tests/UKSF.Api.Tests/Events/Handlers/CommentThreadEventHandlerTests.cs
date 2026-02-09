using Microsoft.AspNetCore.SignalR;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.EventHandlers;
using UKSF.Api.Services;
using UKSF.Api.Signalr.Clients;
using UKSF.Api.Signalr.Hubs;
using Xunit;

namespace UKSF.Api.Tests.Events.Handlers;

public class CommentThreadEventHandlerTests
{
    private readonly CommentThreadEventHandler _commentThreadEventHandler;
    private readonly IEventBus _eventBus;
    private readonly Mock<ICommentThreadClient> _mockClient = new();

    public CommentThreadEventHandlerTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<ICommentThreadService> mockCommentThreadService = new();
        Mock<IUksfLogger> mockLoggingService = new();
        Mock<IHubContext<CommentThreadHub, ICommentThreadClient>> mockHub = new();
        Mock<IHubClients<ICommentThreadClient>> mockHubClients = new();
        _eventBus = new EventBus();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainCommentThread>(It.IsAny<string>()));
        mockCommentThreadService.Setup(x => x.FormatComment(It.IsAny<DomainComment>())).Returns(null);

        mockHub.Setup(x => x.Clients).Returns(mockHubClients.Object);
        mockHubClients.Setup(x => x.Group(It.IsAny<string>())).Returns(_mockClient.Object);

        _commentThreadEventHandler = new CommentThreadEventHandler(_eventBus, mockHub.Object, mockCommentThreadService.Object, mockLoggingService.Object);
    }

    [Fact]
    public void ShouldNotRunEventOnUpdate()
    {
        _commentThreadEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Update, new CommentThreadEventData(string.Empty, new DomainComment()), ""));

        _mockClient.Verify(x => x.ReceiveComment(It.IsAny<object>()), Times.Never);
        _mockClient.Verify(x => x.DeleteComment(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ShouldRunAddedOnAdd()
    {
        _commentThreadEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Add, new CommentThreadEventData(string.Empty, new DomainComment()), ""));

        _mockClient.Verify(x => x.ReceiveComment(It.IsAny<object>()), Times.Once);
        _mockClient.Verify(x => x.DeleteComment(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ShouldRunDeletedOnDelete()
    {
        _commentThreadEventHandler.Init();

        _eventBus.Send(new EventModel(EventType.Delete, new CommentThreadEventData(string.Empty, new DomainComment()), ""));

        _mockClient.Verify(x => x.ReceiveComment(It.IsAny<object>()), Times.Never);
        _mockClient.Verify(x => x.DeleteComment(It.IsAny<string>()), Times.Once);
    }
}
