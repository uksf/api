using UKSF.Api.ArmaServer.Converters;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.Core;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Services;

public interface IPersistenceSessionsService
{
    DomainPersistenceSession Load(string key);
    Task SaveAsync(string key, DomainPersistenceSession session, string sessionId = "");
    Task HandleSaveAsync(string key, string sessionId, object sessionData);
}

public class PersistenceSessionsService(IPersistenceSessionsContext context, IUksfLogger logger) : IPersistenceSessionsService
{
    public DomainPersistenceSession Load(string key)
    {
        return context.GetSingle(x => x.Key == key);
    }

    public async Task SaveAsync(string key, DomainPersistenceSession session, string sessionId = "")
    {
        session.Key = key;
        session.SavedAt = DateTime.UtcNow;

        var existing = context.GetSingle(x => x.Key == key);

        // Track which mission sessions have saved to this persistence key
        if (!string.IsNullOrEmpty(sessionId))
        {
            var sessionIds = existing?.MissionSessionIds ?? [];
            if (!sessionIds.Contains(sessionId))
            {
                sessionIds.Add(sessionId);
            }

            session.MissionSessionIds = sessionIds;
        }
        else if (existing is not null)
        {
            session.MissionSessionIds = existing.MissionSessionIds;
        }

        if (existing is not null)
        {
            session.Id = existing.Id;
            await context.Replace(session);
        }
        else
        {
            await context.Add(session);
        }
    }

    public async Task HandleSaveAsync(string key, string sessionId, object sessionData)
    {
        try
        {
            var rawDict = ToDict(sessionData);
            if (rawDict.Count == 0)
            {
                logger.LogWarning($"persistence_save data was empty for key '{key}'");
                return;
            }

            var session = PersistenceConverter.FromHashmap(rawDict);
            if (session is not null)
            {
                await SaveAsync(key, session, sessionId);
            }
            else
            {
                logger.LogWarning($"Failed to deserialize persistence session for key '{key}'");
            }
        }
        catch (FormatException exception)
        {
            logger.LogError($"Failed to deserialize persistence session for key '{key}'", exception);
        }
    }
}
