using UKSF.Api.ArmaServer.Converters;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.Core;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Services;

public interface IPersistenceSessionsService
{
    DomainPersistenceSession Load(string key);
    Task<bool> SaveAsync(string key, DomainPersistenceSession session, string sessionId = "", DateTime? enqueuedAt = null);
    Task HandleSaveAsync(string key, string sessionId, object sessionData, DateTime? enqueuedAt = null);
}

public class PersistenceSaveRejectedException(string message, Exception innerException = null) : Exception(message, innerException);

public class PersistenceSessionsService(IPersistenceSessionsContext context, IUksfLogger logger) : IPersistenceSessionsService
{
    public DomainPersistenceSession Load(string key)
    {
        return context.GetSingle(x => x.Key == key);
    }

    public async Task<bool> SaveAsync(string key, DomainPersistenceSession session, string sessionId = "", DateTime? enqueuedAt = null)
    {
        session.Key = key;
        session.SavedAt = DateTime.UtcNow;
        session.SaveEnqueuedAt = enqueuedAt;

        var applied = await context.SaveIfNewer(session, sessionId);
        if (!applied)
        {
            logger.LogInfo($"persistence_save for key '{key}' enqueued at {enqueuedAt:O} is not newer than the stored save; ignored");
        }

        return applied;
    }

    public async Task HandleSaveAsync(string key, string sessionId, object sessionData, DateTime? enqueuedAt = null)
    {
        if (sessionData is not Dictionary<string, object> and not List<object>)
        {
            throw new PersistenceSaveRejectedException(
                $"persistence_save data has unexpected type '{sessionData?.GetType().FullName ?? "null"}' for key '{key}'"
            );
        }

        DomainPersistenceSession session;
        try
        {
            var rawDict = ToDict(sessionData);
            if (rawDict.Count == 0)
            {
                throw new PersistenceSaveRejectedException($"persistence_save data was empty for key '{key}'");
            }

            session = PersistenceConverter.FromHashmap(rawDict);
        }
        catch (FormatException exception)
        {
            throw new PersistenceSaveRejectedException($"Failed to deserialize persistence session for key '{key}'", exception);
        }

        if (session is null)
        {
            throw new PersistenceSaveRejectedException($"Failed to deserialize persistence session for key '{key}'");
        }

        await SaveAsync(key, session, sessionId, enqueuedAt);
    }
}
