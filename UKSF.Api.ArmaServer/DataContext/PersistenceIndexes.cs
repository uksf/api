using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.DataContext;

public class PersistenceIndexes(IMongoDatabase database, IUksfLogger logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CreateKeyIndex(database);
        }
        catch (Exception exception)
        {
            logger.LogError("Failed to create persistence indexes", exception);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public static Task CreateKeyIndex(IMongoDatabase database)
    {
        var collection = database.GetCollection<DomainPersistenceSession>("persistenceSessions");
        var model = new CreateIndexModel<DomainPersistenceSession>(
            Builders<DomainPersistenceSession>.IndexKeys.Ascending(x => x.Key),
            new CreateIndexOptions { Name = "ix_key", Unique = true }
        );
        return collection.Indexes.CreateOneAsync(model);
    }
}
