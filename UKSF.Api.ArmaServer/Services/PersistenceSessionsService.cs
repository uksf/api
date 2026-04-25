using System.Text.Json;
using MongoDB.Bson;
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

        try
        {
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
        catch (Exception exception) when (exception is BsonSerializationException or BsonException)
        {
            logger.LogError($"BSON serialization failed for persistence session '{key}', attempting sanitized save", exception);

            try
            {
                SanitizeSession(session);

                if (existing is not null)
                {
                    await context.Replace(session);
                }
                else
                {
                    await context.Add(session);
                }

                logger.LogInfo($"Sanitized persistence session '{key}' saved successfully");
            }
            catch (Exception retryException)
            {
                logger.LogError($"Sanitized save also failed for persistence session '{key}'", retryException);
            }
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

    /// <summary>
    /// Walks the session object graph and converts any remaining <see cref="JsonElement"/>
    /// values to native .NET types that the MongoDB BSON serializer can handle.
    /// This is a last-resort safety net — the <see cref="PersistenceTypeConverter"/> should
    /// prevent <see cref="JsonElement"/> from appearing, but if something slips through,
    /// this ensures the save still succeeds.
    /// </summary>
    private static void SanitizeSession(DomainPersistenceSession session)
    {
        foreach (var player in session.Players.Values)
        {
            player.AceMedical.AdditionalData = SanitizeDictionary(player.AceMedical.AdditionalData);
        }

        session.Markers = session.Markers.Select(SanitizeList).ToList();
        session.CustomData = SanitizeDictionary(session.CustomData);
    }

    private static List<object> SanitizeList(List<object> list) => list.Select(SanitizeValue).ToList();

    private static Dictionary<string, object> SanitizeDictionary(Dictionary<string, object> dictionary)
    {
        return dictionary.ToDictionary(kvp => kvp.Key, kvp => SanitizeValue(kvp.Value));
    }

    private static object SanitizeValue(object value)
    {
        return value switch
        {
            JsonElement element             => ConvertJsonElement(element),
            object[] array                  => array.Select(SanitizeValue).ToArray(),
            List<object> list               => list.Select(SanitizeValue).ToList(),
            Dictionary<string, object> dict => dict.ToDictionary(kvp => kvp.Key, kvp => SanitizeValue(kvp.Value)),
            _                               => value
        };
    }

    private static object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined            => null,
            JsonValueKind.True                                       => true,
            JsonValueKind.False                                      => false,
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number                                     => element.GetDouble(),
            JsonValueKind.String                                     => element.GetString(),
            JsonValueKind.Array                                      => element.EnumerateArray().Select(ConvertJsonElement).ToArray(),
            JsonValueKind.Object                                     => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            _                                                        => element.ToString()
        };
    }
}
