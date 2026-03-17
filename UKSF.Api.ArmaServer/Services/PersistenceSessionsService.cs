using System.Collections.Concurrent;
using System.Text.Json;
using MongoDB.Bson;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Services;

public interface IPersistenceSessionsService
{
    DomainPersistenceSession Load(string key);
    Task SaveAsync(string key, DomainPersistenceSession session);
    Task HandleSaveChunkAsync(ChunkEnvelope chunk);
}

public class PersistenceSessionsService(IPersistenceSessionsContext context, IUksfLogger logger) : IPersistenceSessionsService
{
    // Dedicated options for persistence deserialization.
    // Uses PersistenceTypeConverter to unwrap JsonElement → native .NET types in object fields.
    // Does NOT use InferredTypeConverter (would convert date-like strings to DateTime)
    // Does NOT use DictionaryKeyPolicy (would mutate CustomData keys)
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true, Converters = { new PersistenceTypeConverter() }
    };

    private static readonly TimeSpan ChunkBufferExpiry = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, ChunkBuffer> _chunkBuffers = new();

    public DomainPersistenceSession Load(string key)
    {
        return context.GetSingle(x => x.Key == key);
    }

    public async Task SaveAsync(string key, DomainPersistenceSession session)
    {
        session.Key = key;
        session.SavedAt = DateTime.UtcNow;

        var existing = context.GetSingle(x => x.Key == key);
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

    public async Task HandleSaveChunkAsync(ChunkEnvelope chunk)
    {
        EvictExpiredBuffers();

        var buffer = _chunkBuffers.GetOrAdd(chunk.Id, _ => new ChunkBuffer());
        buffer.Chunks[chunk.Index] = chunk.Data;

        if (buffer.Chunks.Count == chunk.Total && _chunkBuffers.TryRemove(chunk.Id, out var completedBuffer))
        {
            var fullJson = string.Concat(Enumerable.Range(0, chunk.Total).Select(i => completedBuffer.Chunks[i]));

            try
            {
                var session = JsonSerializer.Deserialize<DomainPersistenceSession>(fullJson, SerializerOptions);
                if (session is not null)
                {
                    await SaveAsync(chunk.Key, session);
                }
                else
                {
                    logger.LogWarning($"Failed to deserialize persistence session from chunks for key '{chunk.Key}'");
                }
            }
            catch (JsonException exception)
            {
                logger.LogError($"Failed to deserialize persistence session chunks for key '{chunk.Key}'", exception);
            }
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

    private void EvictExpiredBuffers()
    {
        var cutoff = DateTime.UtcNow - ChunkBufferExpiry;
        foreach (var kvp in _chunkBuffers)
        {
            if (kvp.Value.CreatedAt < cutoff)
            {
                if (_chunkBuffers.TryRemove(kvp.Key, out _))
                {
                    logger.LogWarning($"Evicted incomplete chunk buffer '{kvp.Key}' (created {kvp.Value.CreatedAt:u})");
                }
            }
        }
    }

    private sealed class ChunkBuffer
    {
        public ConcurrentDictionary<int, string> Chunks { get; } = new();
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
    }
}
