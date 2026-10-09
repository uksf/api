using System.Linq.Expressions;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.Core.Context.Base;

namespace UKSF.Api.ArmaServer.DataContext;

public interface IPersistenceSessionsContext : IMongoContext<DomainPersistenceSession>
{
    Task<bool> SaveIfNewer(DomainPersistenceSession session, string sessionId);
}

public class PersistenceSessionsContext(IMongoCollectionFactory mongoCollectionFactory)
    : MongoContextBase<DomainPersistenceSession>(mongoCollectionFactory, "persistenceSessions"), IPersistenceSessionsContext
{
    public async Task<bool> SaveIfNewer(DomainPersistenceSession session, string sessionId)
    {
        var key = session.Key;
        var enqueuedAt = session.SaveEnqueuedAt;
        Expression<Func<DomainPersistenceSession, bool>> filter = enqueuedAt is null
            ? x => x.Key == key
            : x => x.Key == key && (x.SaveEnqueuedAt == null || x.SaveEnqueuedAt < enqueuedAt);

        var update = Builders<DomainPersistenceSession>.Update.Set(x => x.Objects, session.Objects)
                                                       .Set(x => x.DeletedObjects, session.DeletedObjects)
                                                       .Set(x => x.Players, session.Players)
                                                       .Set(x => x.Markers, session.Markers)
                                                       .Set(x => x.ArmaDateTime, session.ArmaDateTime)
                                                       .Set(x => x.CustomData, session.CustomData)
                                                       .Set(x => x.SavedAt, session.SavedAt)
                                                       .Set(x => x.SaveEnqueuedAt, enqueuedAt);
        if (!string.IsNullOrEmpty(sessionId))
        {
            update = update.AddToSet(x => x.MissionSessionIds, sessionId);
        }

        try
        {
            await Upsert(filter, update);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return await FindAndUpdate(filter, update);
        }
    }
}
