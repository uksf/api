using System.Collections.Concurrent;
using System.Text.Json;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Services;

public interface IPersistenceSessionsService
{
    DomainPersistenceSession? Load(string key);
    Task SaveAsync(string key, DomainPersistenceSession session);
    Task HandleSaveChunkAsync(ChunkEnvelope chunk);
}

public class PersistenceSessionsService(IPersistenceSessionsContext context, IUksfLogger logger) : IPersistenceSessionsService
{
    private readonly ConcurrentDictionary<string, Dictionary<int, string>> _chunkBuffers = new();

    public DomainPersistenceSession? Load(string key)
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
        var buffer = _chunkBuffers.GetOrAdd(chunk.Id, _ => new Dictionary<int, string>());

        buffer[chunk.Index] = chunk.Data;

        if (buffer.Count == chunk.Total)
        {
            _chunkBuffers.TryRemove(chunk.Id, out _);

            var fullJson = string.Concat(Enumerable.Range(0, chunk.Total).Select(i => buffer[i]));

            try
            {
                var session = JsonSerializer.Deserialize<DomainPersistenceSession>(fullJson);
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
}
