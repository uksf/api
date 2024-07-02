using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Core;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Services;
using UKSF.Api.Signalr.Clients;
using UKSF.Api.Signalr.Hubs;

namespace UKSF.Api.EventHandlers;

public interface ICommentThreadEventHandler : IEventHandler;

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
            case EventType.Add:
                await AddedEvent(commentThreadEventData.CommentThreadId, commentThreadEventData.Comment);
                break;
            case EventType.Delete:
                await DeletedEvent(commentThreadEventData.CommentThreadId, commentThreadEventData.Comment);
                break;
            case EventType.Update: break;
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
