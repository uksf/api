﻿using MongoDB.Driver;
using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context;

public interface ICommentThreadContext : IMongoContext<CommentThread>, ICachedMongoContext
{
    Task AddCommentToThread(string commentThreadId, Comment comment);
    Task RemoveCommentFromThread(string commentThreadId, Comment comment);
}

public class CommentThreadContext : CachedMongoContext<CommentThread>, ICommentThreadContext
{
    public CommentThreadContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) :
        base(mongoCollectionFactory, eventBus, "commentThreads") { }

    public async Task AddCommentToThread(string commentThreadId, Comment comment)
    {
        var updateDefinition = Builders<CommentThread>.Update.Push(x => x.Comments, comment);
        await base.Update(commentThreadId, updateDefinition);
        CommentThreadDataEvent(new(EventType.ADD, new CommentThreadEventData(commentThreadId, comment)));
    }

    public async Task RemoveCommentFromThread(string commentThreadId, Comment comment)
    {
        var updateDefinition = Builders<CommentThread>.Update.Pull(x => x.Comments, comment);
        await base.Update(commentThreadId, updateDefinition);
        CommentThreadDataEvent(new(EventType.DELETE, new CommentThreadEventData(commentThreadId, comment)));
    }

    private void CommentThreadDataEvent(EventModel eventModel)
    {
        base.DataEvent(eventModel);
    }

    protected override void DataEvent(EventModel eventModel) { }
}