using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Personnel.Signalr.Clients;
using UKSF.Api.Personnel.Signalr.Hubs;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Extensions;

namespace UKSF.Api.Personnel.EventHandlers;

public interface ICommentThreadEventHandler : IEventHandler { }

public class CommentThreadEventHandler : ICommentThreadEventHandler
{
    private readonly ICommentThreadService _commentThreadService;
    private readonly IEventBus _eventBus;
    private readonly IHubContext<CommentThreadHub, ICommentThreadClient> _hub;
    private readonly IUksfLogger _logger;

    public CommentThreadEventHandler(
        IEventBus eventBus,
        IHubContext<CommentThreadHub, ICommentThreadClient> hub,
        ICommentThreadService commentThreadService,
        IUksfLogger logger
    )
    {
        _eventBus = eventBus;
        _hub = hub;
        _commentThreadService = commentThreadService;
        _logger = logger;
    }

    public void EarlyInit() { }

    public void Init()
    {
        _eventBus.AsObservable().SubscribeWithAsyncNext<CommentThreadEventData>(HandleEvent, _logger.LogError);
    }

    private async Task HandleEvent(EventModel eventModel, CommentThreadEventData commentThreadEventData)
    {
        switch (eventModel.EventType)
        {
            case EventType.ADD:
                await AddedEvent(commentThreadEventData.CommentThreadId, commentThreadEventData.Comment);
                break;
            case EventType.DELETE:
                await DeletedEvent(commentThreadEventData.CommentThreadId, commentThreadEventData.Comment);
                break;
            case EventType.UPDATE: break;
        }
    }

    private Task AddedEvent(string id, Comment comment)
    {
        return _hub.Clients.Group(id).ReceiveComment(_commentThreadService.FormatComment(comment));
    }

    private Task DeletedEvent(string id, MongoObject comment)
    {
        return _hub.Clients.Group(id).DeleteComment(comment.Id);
    }
}
