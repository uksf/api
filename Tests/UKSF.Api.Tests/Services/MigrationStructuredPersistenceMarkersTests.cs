using System;
using System.Threading.Tasks;
using FluentAssertions;
using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public sealed class MigrationStructuredPersistenceMarkersTests : IDisposable
{
    private readonly MongoDbRunner _runner = MongoDbRunner.Start(singleNodeReplSet: false);
    private readonly IMongoDatabase _database;
    private readonly Mock<IMigrationContext> _migrationContext = new();

    public MigrationStructuredPersistenceMarkersTests()
    {
        _database = new MongoClient(_runner.ConnectionString).GetDatabase("test");
    }

    public void Dispose() => _runner.Dispose();

    [Fact]
    public async Task RunMigrations_ConvertsLegacyMarkerArraysAndRecordsVersion14()
    {
        var sessions = _database.GetCollection<BsonDocument>("persistenceSessions");
        await sessions.InsertOneAsync(
            new BsonDocument
            {
                { "key", "legacy" },
                { "markers", new BsonArray { new BsonArray { "line", new BsonArray { 1.5, 2.5, 0L }, "POLYLINE", "ColorRed", 1L, new BsonArray { 1.5, 2.5 } } } }
            }
        );

        await new MigrationUtility(_migrationContext.Object, _database, new Mock<IUksfLogger>().Object).RunMigrations();

        var marker = (await sessions.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync())["markers"][0];
        marker.IsBsonDocument.Should().BeTrue();
        _migrationContext.Verify(x => x.Add(It.Is<Migration>(m => m.Version == 14)), Times.Once);
    }
}
