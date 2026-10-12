using System;
using System.Collections.Generic;
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

public sealed class PersistenceSaveMongoTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 10, 9, 18, 14, 21, 108, DateTimeKind.Utc);

    private readonly MongoDbRunner _runner = MongoDbRunner.Start(singleNodeReplSet: false);
    private readonly IMongoDatabase _database;
    private readonly PersistenceSessionsService _subject;

    public PersistenceSaveMongoTests()
    {
        _database = new MongoClient(_runner.ConnectionString).GetDatabase($"persistence-{ObjectId.GenerateNewId()}");
        _subject = new PersistenceSessionsService(
            new PersistenceSessionsContext(new MongoCollectionFactory(_database), _database),
            new Mock<IUksfLogger>().Object
        );
    }

    public void Dispose() => _runner.Dispose();

    private static readonly PersistenceMarker Marker = new() { Name = "marker_1", Position = [1.5, 2.5, 0], Shape = "ICON", Type = "hd_dot", Brush = "Solid", Size = [1, 1], Colour = "ColorRed", Alpha = 1, Direction = 0, Text = "" };

    private static DomainPersistenceSession Save(string marker) =>
        new()
        {
            DeletedObjects = [marker],
            Markers = [Marker],
            CustomData = new Dictionary<string, object>
            {
                ["marker"] = marker,
                ["nested"] = new List<object>
                {
                    1L,
                    "two",
                    new List<object> { true }
                }
            }
        };

    private List<DomainPersistenceSession> Stored(string key) =>
        _database.GetCollection<DomainPersistenceSession>("persistenceSessions").Find(x => x.Key == key).ToList();

    [Fact]
    public async Task NewerSave_ReplacesContentAndAccumulatesSessionIds()
    {
        await _subject.SaveAsync("k", Save("first"), "session-a", T0);

        var applied = await _subject.SaveAsync("k", Save("second"), "session-b", T0.AddSeconds(1));

        applied.Should().BeTrue();
        var stored = Stored("k").Should().ContainSingle().Subject;
        stored.DeletedObjects.Should().Equal("second");
        stored.SaveEnqueuedAt.Should().Be(T0.AddSeconds(1));
        stored.MissionSessionIds.Should().Equal("session-a", "session-b");
    }

    [Fact]
    public async Task RetriedDelivery_WithSameEnqueueTime_IsAppliedOnce()
    {
        await _subject.SaveAsync("k", Save("original"), "session-a", T0);

        var applied = await _subject.SaveAsync("k", Save("retry"), "session-a", T0);

        applied.Should().BeFalse();
        Stored("k").Should().ContainSingle().Which.DeletedObjects.Should().Equal("original");
    }

    [Fact]
    public async Task DelayedOlderSave_DoesNotOverwriteNewerSave()
    {
        await _subject.SaveAsync("k", Save("newer"), "session-b", T0.AddMinutes(5));

        var applied = await _subject.SaveAsync("k", Save("older"), "session-a", T0);

        applied.Should().BeFalse();
        var stored = Stored("k").Should().ContainSingle().Subject;
        stored.DeletedObjects.Should().Equal("newer");
        stored.MissionSessionIds.Should().Equal("session-b");
    }

    [Fact]
    public async Task UnstampedSave_OverwritesUnconditionally()
    {
        await _subject.SaveAsync("k", Save("stamped"), "session-a", T0);

        var applied = await _subject.SaveAsync("k", Save("unstamped"), "session-b");

        applied.Should().BeTrue();
        Stored("k").Should().ContainSingle().Which.DeletedObjects.Should().Equal("unstamped");
    }

    [Fact]
    public async Task LegacyDocumentWithoutStampField_IsOverwrittenByStampedSave()
    {
        await _database.GetCollection<BsonDocument>("persistenceSessions")
                       .InsertOneAsync(
                           new BsonDocument
                           {
                               ["key"] = "k",
                               ["deletedObjects"] = new BsonArray { "legacy" },
                               ["missionSessionIds"] = new BsonArray { "old" }
                           }
                       );

        var applied = await _subject.SaveAsync("k", Save("stamped"), "session-a", T0);

        applied.Should().BeTrue();
        var stored = Stored("k").Should().ContainSingle().Subject;
        stored.DeletedObjects.Should().Equal("stamped");
        stored.MissionSessionIds.Should().Equal("old", "session-a");
    }

    [Fact]
    public async Task UnstampedSave_KeepsTheStoredStampSoAnOlderRetryCannotOverwriteIt()
    {
        await _subject.SaveAsync("k", Save("stamped"), "session-a", T0.AddMinutes(5));
        await _subject.SaveAsync("k", Save("unstamped"), "session-a");

        var applied = await _subject.SaveAsync("k", Save("older"), "session-a", T0);

        applied.Should().BeFalse();
        var stored = Stored("k").Should().ContainSingle().Subject;
        stored.DeletedObjects.Should().Equal("unstamped");
        stored.SaveEnqueuedAt.Should().Be(T0.AddMinutes(5));
    }

    [Fact]
    public async Task SavedMarkersAndCustomData_RoundTripThroughTheProductionSerializers()
    {
        await _subject.SaveAsync("k", Save("first"), "session-a", T0);

        var stored = _subject.Load("k");

        stored.Markers.Should().ContainSingle().Which.Should().BeEquivalentTo(Marker);
        stored.CustomData["nested"]
        .Should()
        .BeEquivalentTo(
            new List<object>
            {
                1L,
                "two",
                new List<object> { true }
            }
        );
    }

    [Fact]
    public async Task ConcurrentFirstSaves_KeepOneDocumentWithTheNewestContent()
    {
        for (var round = 0; round < 20; round++)
        {
            var key = $"race-{round}";
            var saves = Enumerable.Range(0, 8).Select(i => _subject.SaveAsync(key, Save($"save-{i}"), $"session-{i}", T0.AddSeconds(i)));

            await Task.WhenAll(saves);

            Stored(key).Should().ContainSingle().Which.DeletedObjects.Should().Equal("save-7");
        }
    }
}
