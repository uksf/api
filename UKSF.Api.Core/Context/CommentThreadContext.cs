using MongoDB.Driver;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface ICommentThreadContext : IMongoContext<CommentThread>, ICachedMongoContext
{
    Task AddCommentToThread(string commentThreadId, Comment comment);
    Task RemoveCommentFromThread(string commentThreadId, Comment comment);
}

public class CommentThreadContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<CommentThread>(mongoCollectionFactory, eventBus, variablesService, "commentThreads"), ICommentThreadContext
{
    public async Task AddCommentToThread(string commentThreadId, Comment comment)
    {
        var updateDefinition = Builders<CommentThread>.Update.Push(x => x.Comments, comment);
        await base.Update(commentThreadId, updateDefinition);
        CommentThreadDataEvent(EventType.Add, new CommentThreadEventData(commentThreadId, comment));
    }

    public async Task RemoveCommentFromThread(string commentThreadId, Comment comment)
    {
        var updateDefinition = Builders<CommentThread>.Update.Pull(x => x.Comments, comment);
        await base.Update(commentThreadId, updateDefinition);
        CommentThreadDataEvent(EventType.Delete, new CommentThreadEventData(commentThreadId, comment));
    }

    private void CommentThreadDataEvent(EventType eventType, EventData eventData)
    {
        base.DataEvent(eventType, eventData);
    }

    protected override void DataEvent(EventType eventType, EventData eventData) { }
}
