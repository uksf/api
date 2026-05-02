using System.Text.Json;
using UKSF.Api.ArmaServer.Converters;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.ArmaServer.Parsing;
using UKSF.Api.Core;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

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

    public async Task HandleSaveAsync(string key, string sessionId, string payload)
    {
        try
        {
            var session = ParsePayload(payload);
            if (session is not null)
            {
                await SaveAsync(key, session, sessionId);
            }
            else
            {
                logger.LogWarning($"Failed to deserialize persistence session for key '{key}'");
            }
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            logger.LogError($"Failed to deserialize persistence session for key '{key}'", exception);
        }
    }

    private static DomainPersistenceSession ParsePayload(string payload)
    {
        // SQF `str` output of the session HashMap starts with '['.
        // Legacy JSON object payload starts with '{'.
        // Skip leading whitespace before deciding.
        var firstNonWs = payload.AsSpan().TrimStart();
        if (firstNonWs.IsEmpty) return null;

        if (firstNonWs[0] == '[')
        {
            var raw = ToDict(SqfNotationParser.ParseAndNormalize(payload));
            return PersistenceConverter.FromHashmap(raw);
        }

        var rawDict = JsonSerializer.Deserialize<Dictionary<string, object>>(payload, SerializerOptions);
        if (rawDict is not null && rawDict.ContainsKey("mapMarkers"))
        {
            return PersistenceConverter.FromHashmap(rawDict);
        }

        return JsonSerializer.Deserialize<DomainPersistenceSession>(payload, SerializerOptions);
    }
}
