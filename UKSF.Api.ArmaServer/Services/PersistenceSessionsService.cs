using System.Collections.Concurrent;
using System.Text.Json;
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
    // Does NOT use InferredTypeConverter (would convert date-like strings to DateTime in object fields)
    // Does NOT use DictionaryKeyPolicy (would mutate CustomData keys)
    private static readonly JsonSerializerOptions DeserializerOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly TimeSpan ChunkBufferExpiry = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, ChunkBuffer> _chunkBuffers = new();

    public DomainPersistenceSession Load(string key)
    {
        return context.Get(x => x.Key == key).FirstOrDefault();
    }

    public async Task SaveAsync(string key, DomainPersistenceSession session)
    {
        session.Key = key;
        session.SavedAt = DateTime.UtcNow;

        var existing = context.Get(x => x.Key == key).FirstOrDefault();
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
                var session = JsonSerializer.Deserialize<DomainPersistenceSession>(fullJson, DeserializerOptions);
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
