using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class MigrationStatsBatchesSplitTests : IDisposable
{
    private readonly MongoDbRunner _runner = MongoDbRunner.Start(singleNodeReplSet: false);
    private readonly IMongoDatabase _database;
    private readonly Mock<IMigrationContext> _migrationContextMock = new();
    private readonly Mock<IUksfLogger> _loggerMock = new();
    private readonly MigrationUtility _sut;

    public MigrationStatsBatchesSplitTests()
    {
        _database = new MongoClient(_runner.ConnectionString).GetDatabase("test");
        _sut = new MigrationUtility(_migrationContextMock.Object, _database, _loggerMock.Object);
    }

    public void Dispose()
    {
        _runner.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Migration_SplitsLegacyBatchIntoThreeCollections()
    {
        var batches = _database.GetCollection<BsonDocument>("missionStatsBatches");
        await batches.InsertOneAsync(
            new BsonDocument
            {
                { "missionSessionId", "session-A" },
                { "receivedAt", new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc) },
                {
                    "events", new BsonArray
                    {
                        new BsonDocument
                        {
                            { "type", "samplerBatch" },
                            { "uid", "u1" },
                            { "timestamp", "2024-01-01T10:05:00Z" },
                            { "distanceOnFoot", new BsonArray { 1.0, 2.5 } }
                        },
                        new BsonDocument { { "type", "shot" }, { "uid", "u1" } },
                        new BsonDocument { { "type", "unconscious" }, { "uid", "u1" } }
                    }
                }
            }
        );

        await InvokeMigrateStatsBatchesSplit();

        var sampler = await _database.GetCollection<BsonDocument>("missionStatsEventsSampler").Find(new BsonDocument()).ToListAsync();
        sampler.Should().HaveCount(1);
        sampler[0]["missionSessionId"].AsString.Should().Be("session-A");
        sampler[0]["playerUid"].AsString.Should().Be("u1");
        sampler[0]["distanceOnFoot"].AsBsonArray.Select(x => x.ToDouble()).Should().BeEquivalentTo(new[] { 1.0, 2.5 });

        var combat = await _database.GetCollection<BsonDocument>("missionStatsEventsCombat").Find(new BsonDocument()).ToListAsync();
        combat.Should().HaveCount(1);
        combat[0]["missionSessionId"].AsString.Should().Be("session-A");
        combat[0]["bucketIndex"].ToInt32().Should().Be(1);
        combat[0]["eventCount"].ToInt32().Should().Be(1);
        combat[0]["events"].AsBsonArray.Should().HaveCount(1);
        combat[0]["events"].AsBsonArray[0].AsBsonDocument["type"].AsString.Should().Be("shot");

        var lifecycle = await _database.GetCollection<BsonDocument>("missionStatsEventsLifecycle").Find(new BsonDocument()).ToListAsync();
        lifecycle.Should().HaveCount(1);
        lifecycle[0]["missionSessionId"].AsString.Should().Be("session-A");
        lifecycle[0]["events"].AsBsonArray.Should().HaveCount(1);
        lifecycle[0]["events"].AsBsonArray[0].AsBsonDocument["type"].AsString.Should().Be("unconscious");

        var collections = await _database.ListCollectionNames().ToListAsync();
        collections.Should().NotContain("missionStatsBatches");
    }

    [Fact]
    public async Task Migration_BatchesPerSessionAreConcatenatedInReceivedAtOrder()
    {
        var batches = _database.GetCollection<BsonDocument>("missionStatsBatches");
        await batches.InsertOneAsync(
            new BsonDocument
            {
                { "missionSessionId", "session-B" },
                { "receivedAt", new DateTime(2024, 1, 1, 10, 30, 0, DateTimeKind.Utc) },
                { "events", new BsonArray { new BsonDocument { { "type", "shot" }, { "marker", "second" } } } }
            }
        );
        await batches.InsertOneAsync(
            new BsonDocument
            {
                { "missionSessionId", "session-B" },
                { "receivedAt", new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc) },
                { "events", new BsonArray { new BsonDocument { { "type", "shot" }, { "marker", "first" } } } }
            }
        );

        await InvokeMigrateStatsBatchesSplit();

        var combat = await _database.GetCollection<BsonDocument>("missionStatsEventsCombat").Find(new BsonDocument()).ToListAsync();
        combat.Should().HaveCount(1);
        var events = combat[0]["events"].AsBsonArray;
        events.Should().HaveCount(2);
        events[0].AsBsonDocument["marker"].AsString.Should().Be("first");
        events[1].AsBsonDocument["marker"].AsString.Should().Be("second");
    }

    [Fact]
    public async Task Migration_CombatEventsExceedingBucketSize_SplitIntoMultipleBuckets()
    {
        var batches = _database.GetCollection<BsonDocument>("missionStatsBatches");
        var totalEvents = MissionStatsEventsCombat.MaxEventsPerBucket + 50;
        var eventsArray = new BsonArray();
        for (var i = 0; i < totalEvents; i++)
        {
            eventsArray.Add(new BsonDocument { { "type", "shot" }, { "ix", i } });
        }

        await batches.InsertOneAsync(
            new BsonDocument
            {
                { "missionSessionId", "session-C" },
                { "receivedAt", DateTime.UtcNow },
                { "events", eventsArray }
            }
        );

        await InvokeMigrateStatsBatchesSplit();

        var combat = await _database.GetCollection<BsonDocument>("missionStatsEventsCombat")
                                    .Find(new BsonDocument())
                                    .SortBy(b => b["bucketIndex"])
                                    .ToListAsync();
        combat.Should().HaveCount(2);
        combat[0]["bucketIndex"].ToInt32().Should().Be(1);
        combat[0]["eventCount"].ToInt32().Should().Be(MissionStatsEventsCombat.MaxEventsPerBucket);
        combat[0]["events"].AsBsonArray.Should().HaveCount(MissionStatsEventsCombat.MaxEventsPerBucket);
        combat[1]["bucketIndex"].ToInt32().Should().Be(2);
        combat[1]["eventCount"].ToInt32().Should().Be(50);
        combat[1]["events"].AsBsonArray.Should().HaveCount(50);
    }

    [Fact]
    public async Task Migration_DoesNotOverwriteExistingNewCollectionDocs()
    {
        var batches = _database.GetCollection<BsonDocument>("missionStatsBatches");
        await batches.InsertOneAsync(
            new BsonDocument
            {
                { "missionSessionId", "session-D" },
                { "receivedAt", DateTime.UtcNow },
                {
                    "events", new BsonArray
                    {
                        new BsonDocument
                        {
                            { "type", "samplerBatch" },
                            { "uid", "u1" },
                            { "distanceOnFoot", new BsonArray { 100.0 } }
                        }
                    }
                }
            }
        );

        var existingSamplerId = ObjectId.GenerateNewId().ToString();
        await _database.GetCollection<BsonDocument>("missionStatsEventsSampler")
                       .InsertOneAsync(
                           new BsonDocument
                           {
                               { "_id", existingSamplerId },
                               { "missionSessionId", "session-D" },
                               { "playerUid", "u1" },
                               { "distanceOnFoot", new BsonArray { 999.0 } },
                               { "distanceInVehicle", new BsonArray() },
                               { "fuelLitres", new BsonArray() }
                           }
                       );

        await InvokeMigrateStatsBatchesSplit();

        var sampler = await _database.GetCollection<BsonDocument>("missionStatsEventsSampler").Find(new BsonDocument()).ToListAsync();
        sampler.Should().HaveCount(1);
        sampler[0]["_id"].AsString.Should().Be(existingSamplerId);
        sampler[0]["distanceOnFoot"].AsBsonArray.Select(x => x.ToDouble()).Should().BeEquivalentTo(new[] { 999.0 });
    }

    [Fact]
    public async Task Migration_DropsLegacyCollectionAfterSuccess()
    {
        var batches = _database.GetCollection<BsonDocument>("missionStatsBatches");
        await batches.InsertOneAsync(
            new BsonDocument
            {
                { "missionSessionId", "session-E" },
                { "receivedAt", DateTime.UtcNow },
                { "events", new BsonArray() }
            }
        );

        await InvokeMigrateStatsBatchesSplit();

        var collections = await _database.ListCollectionNames().ToListAsync();
        collections.Should().NotContain("missionStatsBatches");
    }

    [Fact]
    public async Task Migration_WrittenDocsAreReadableViaTypedContext()
    {
        var batches = _database.GetCollection<BsonDocument>("missionStatsBatches");
        await batches.InsertOneAsync(
            new BsonDocument
            {
                { "missionSessionId", "session-typed" },
                { "receivedAt", new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc) },
                {
                    "events", new BsonArray
                    {
                        new BsonDocument
                        {
                            { "type", "samplerBatch" },
                            { "uid", "u1" },
                            { "timestamp", "2024-01-01T10:05:00Z" },
                            { "distanceOnFoot", new BsonArray { 1.0, 2.5 } }
                        },
                        new BsonDocument { { "type", "shot" }, { "uid", "u1" } },
                        new BsonDocument { { "type", "unconscious" }, { "uid", "u1" } }
                    }
                }
            }
        );

        await InvokeMigrateStatsBatchesSplit();

        var rawSampler = await _database.GetCollection<BsonDocument>("missionStatsEventsSampler").Find(_ => true).FirstAsync();
        rawSampler["_id"].BsonType.Should().Be(BsonType.ObjectId, "raw _id must be stored as a BSON ObjectId so the typed driver can deserialise it");

        var samplerDoc = await _database.GetCollection<MissionStatsEventsSampler>("missionStatsEventsSampler").Find(_ => true).FirstAsync();
        var combatDoc = await _database.GetCollection<MissionStatsEventsCombat>("missionStatsEventsCombat").Find(_ => true).FirstAsync();
        var lifecycleDoc = await _database.GetCollection<MissionStatsEventsLifecycle>("missionStatsEventsLifecycle").Find(_ => true).FirstAsync();

        samplerDoc.Id.Should().NotBeNullOrEmpty();
        samplerDoc.Id!.Length.Should().Be(24);
        samplerDoc.MissionSessionId.Should().Be("session-typed");
        samplerDoc.PlayerUid.Should().Be("u1");

        combatDoc.Id.Should().NotBeNullOrEmpty();
        combatDoc.Id!.Length.Should().Be(24);
        combatDoc.MissionSessionId.Should().Be("session-typed");
        combatDoc.BucketIndex.Should().Be(1);

        lifecycleDoc.Id.Should().NotBeNullOrEmpty();
        lifecycleDoc.Id!.Length.Should().Be(24);
        lifecycleDoc.MissionSessionId.Should().Be("session-typed");
    }

    [Fact]
    public async Task Migration_IsVersionGated_DoesNotRunTwice()
    {
        var batches = _database.GetCollection<BsonDocument>("missionStatsBatches");
        await batches.InsertOneAsync(
            new BsonDocument
            {
                { "missionSessionId", "session-F" },
                { "receivedAt", DateTime.UtcNow },
                { "events", new BsonArray { new BsonDocument { { "type", "shot" } } } }
            }
        );

        _migrationContextMock.Setup(x => x.GetSingle(It.IsAny<Func<Migration, bool>>())).Returns(new Migration { Version = 11 });

        await _sut.RunMigrations();

        var combat = await _database.GetCollection<BsonDocument>("missionStatsEventsCombat").Find(new BsonDocument()).ToListAsync();
        combat.Should().BeEmpty();
        var legacy = await _database.GetCollection<BsonDocument>("missionStatsBatches").Find(new BsonDocument()).ToListAsync();
        legacy.Should().HaveCount(1);
        _migrationContextMock.Verify(x => x.Add(It.IsAny<Migration>()), Times.Never);
    }

    private async Task InvokeMigrateStatsBatchesSplit()
    {
        var method = typeof(MigrationUtility).GetMethod("MigrateStatsBatchesSplit", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull("the migration step must be implemented");
        await (Task)method!.Invoke(_sut, [])!;
    }
}
