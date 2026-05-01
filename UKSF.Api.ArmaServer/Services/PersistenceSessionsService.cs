using System.Text.Json;
using UKSF.Api.ArmaServer.Converters;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Services;

public interface IPersistenceSessionsService
{
    DomainPersistenceSession Load(string key);
    Task SaveAsync(string key, DomainPersistenceSession session, string sessionId = "");
    Task HandleSaveAsync(string key, string sessionId, string json);
}

public class PersistenceSessionsService(IPersistenceSessionsContext context, IUksfLogger logger) : IPersistenceSessionsService
{
    // Dedicated options for persistence deserialization.
    // Uses PersistenceTypeConverter to unwrap JsonElement → native .NET types in object fields.
    // Does NOT use InferredTypeConverter (would convert date-like strings to DateTime)
    // Does NOT use DictionaryKeyPolicy (would mutate CustomData keys)
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new PersistenceTypeConverter(),
            new WoundEntryConverter(),
            new MedicationEntryConverter(),
            new OccludedMedicationEntryConverter(),
            new IvBagEntryConverter(),
            new TriageCardEntryConverter(),
            new MedicalLogCategoryConverter(),
            new MedicalLogEntryConverter()
        }
    };

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

    public async Task HandleSaveAsync(string key, string sessionId, string json)
    {
        try
        {
            var rawDict = JsonSerializer.Deserialize<Dictionary<string, object>>(json, SerializerOptions);
            DomainPersistenceSession session;
            if (rawDict is not null && rawDict.ContainsKey("mapMarkers"))
            {
                session = PersistenceConverter.FromHashmap(rawDict);
            }
            else
            {
                session = JsonSerializer.Deserialize<DomainPersistenceSession>(json, SerializerOptions);
            }

            if (session is not null)
            {
                await SaveAsync(key, session, sessionId);
            }
            else
            {
                logger.LogWarning($"Failed to deserialize persistence session for key '{key}'");
            }
        }
        catch (JsonException exception)
        {
            logger.LogError($"Failed to deserialize persistence session for key '{key}'", exception);
        }
    }
}
