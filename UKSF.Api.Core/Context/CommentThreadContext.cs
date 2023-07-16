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

public class CommentThreadContext : CachedMongoContext<CommentThread>, ICommentThreadContext
{
    public CommentThreadContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService) : base(
        mongoCollectionFactory,
        eventBus,
        variablesService,
        "commentThreads"
    ) { }

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
