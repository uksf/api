using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Context.Base;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Integration;

public sealed class PersistenceMarkerMongoTests : IDisposable
{
    private readonly MongoDbRunner _runner = MongoDbRunner.Start(singleNodeReplSet: false);
    private readonly IMongoDatabase _database;
    private readonly MongoDB.Driver.IMongoCollection<BsonDocument> _raw;
    private readonly PersistenceSessionsService _subject;

    public PersistenceMarkerMongoTests()
    {
        _database = new MongoClient(_runner.ConnectionString).GetDatabase($"markers-{ObjectId.GenerateNewId()}");
        _raw = _database.GetCollection<BsonDocument>("persistenceSessions");
        _subject = new PersistenceSessionsService(
            new PersistenceSessionsContext(new MongoCollectionFactory(_database), _database),
            new Mock<IUksfLogger>().Object
        );
    }

    public void Dispose() => _runner.Dispose();

    private static BsonArray LegacyMarkers() =>
    [
        new BsonArray { "icon", new BsonArray { 18642L, 1641.53, 0L }, "ICON", "GRG_Marker", "Solid", new BsonArray { 1L, 1L }, "ColorGreen", 1L, 0L, "X-2" },
        new BsonArray { "line", new BsonArray { 1.5, 2.5, 0L }, "POLYLINE", "ColorRed", 1L, new BsonArray { 1.5, 2.5, 3.5, 4.5 } }
    ];

    private async Task InsertLegacy(string key) =>
        await _raw.InsertOneAsync(
            new BsonDocument
            {
                { "key", key },
                { "markers", LegacyMarkers() },
                { "deletedObjects", new BsonArray { "kept" } }
            }
        );

    private BsonDocument RawDocument(string key) => _raw.Find(Builders<BsonDocument>.Filter.Eq("key", key)).Single();

    [Fact]
    public async Task LegacyArrayMarkers_LoadAsTypedMarkers()
    {
        await InsertLegacy("legacy");

        var markers = _subject.Load("legacy").Markers;

        markers.Select(x => x.Name).Should().Equal("icon", "line");
        markers[0].Position.Should().Equal(18642, 1641.53, 0);
        markers[0].Text.Should().Be("X-2");
        markers[1].Polyline.Should().Equal(1.5, 2.5, 3.5, 4.5);
        markers[1].Type.Should().BeNull();
    }

    [Fact]
    public async Task SavedMarkers_AreStoredAsNamedFields()
    {
        await InsertLegacy("legacy");
        var session = _subject.Load("legacy");

        await _subject.SaveAsync("saved", session, "session-a", DateTime.UtcNow);

        var stored = RawDocument("saved")["markers"].AsBsonArray;
        stored[0].AsBsonDocument["name"].AsString.Should().Be("icon");
        stored[0].AsBsonDocument["direction"].ToDouble().Should().Be(0);
        stored[1].AsBsonDocument["polyline"].AsBsonArray.Should().HaveCount(4);
        stored[1].AsBsonDocument.Contains("text").Should().BeFalse();
    }

    [Fact]
    public async Task Migration_ConvertsLegacyMarkersAndLeavesOtherFieldsAlone()
    {
        await InsertLegacy("legacy");

        var migrated = await PersistenceMarkerMigration.MigrateAsync(_database);
        var again = await PersistenceMarkerMigration.MigrateAsync(_database);

        migrated.Should().Be(1);
        again.Should().Be(0);
        var document = RawDocument("legacy");
        document["markers"].AsBsonArray.Select(x => x.AsBsonDocument["name"].AsString).Should().Equal("icon", "line");
        document["deletedObjects"].AsBsonArray.Select(x => x.AsString).Should().Equal("kept");
        _subject.Load("legacy").Markers[1].Polyline.Should().Equal(1.5, 2.5, 3.5, 4.5);
    }
}
