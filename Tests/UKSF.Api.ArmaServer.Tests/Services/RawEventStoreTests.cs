using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class RawEventStoreTests
{
    static RawEventStoreTests()
    {
        ConventionRegistry.Register("TestCamelCase", new ConventionPack { new CamelCaseElementNameConvention() }, _ => true);
    }

    private readonly Mock<IMissionStatsEventsSamplerContext> _samplerContext = new();
    private readonly Mock<IMissionStatsEventsCombatContext> _combatContext = new();
    private readonly Mock<IMissionStatsEventsLifecycleContext> _lifecycleContext = new();

    private readonly RawEventStore _subject;

    public RawEventStoreTests()
    {
        _subject = new RawEventStore(_samplerContext.Object, _combatContext.Object, _lifecycleContext.Object);
    }

    private static BsonDocument RenderSampler(UpdateDefinition<MissionStatsEventsSampler> update)
    {
        var serializer = BsonSerializer.LookupSerializer<MissionStatsEventsSampler>();
        return update.Render(new RenderArgs<MissionStatsEventsSampler>(serializer, BsonSerializer.SerializerRegistry)).AsBsonDocument;
    }

    private static BsonDocument RenderCombat(UpdateDefinition<MissionStatsEventsCombat> update)
    {
        var serializer = BsonSerializer.LookupSerializer<MissionStatsEventsCombat>();
        return update.Render(new RenderArgs<MissionStatsEventsCombat>(serializer, BsonSerializer.SerializerRegistry)).AsBsonDocument;
    }

    private static BsonDocument RenderLifecycle(UpdateDefinition<MissionStatsEventsLifecycle> update)
    {
        var serializer = BsonSerializer.LookupSerializer<MissionStatsEventsLifecycle>();
        return update.Render(new RenderArgs<MissionStatsEventsLifecycle>(serializer, BsonSerializer.SerializerRegistry)).AsBsonDocument;
    }

    private static IEnumerable<double> ExtractPushedDoubles(BsonDocument rendered, string field)
    {
        var pushed = rendered["$push"].AsBsonDocument[field];
        if (pushed.IsBsonDocument && pushed.AsBsonDocument.Contains("$each"))
        {
            return pushed.AsBsonDocument["$each"].AsBsonArray.Select(x => x.ToDouble());
        }

        return [pushed.ToDouble()];
    }

    private static IEnumerable<BsonValue> ExtractPushedValues(BsonDocument rendered, string field)
    {
        var pushed = rendered["$push"].AsBsonDocument[field];
        if (pushed.IsBsonDocument && pushed.AsBsonDocument.Contains("$each"))
        {
            return pushed.AsBsonDocument["$each"].AsBsonArray;
        }

        return [pushed];
    }

    private static BsonDocument SamplerEvent(string uid, double[] distanceOnFoot)
    {
        var doc = new BsonDocument
        {
            { "type", "samplerBatch" },
            { "uid", uid },
            { "distanceOnFoot", new BsonArray(distanceOnFoot.Select(v => (BsonValue)v)) }
        };
        return doc;
    }

    [Fact]
    public async Task StoreAsync_SamplerEvent_FoldsByPlayerUid_ConcatenatingSeriesArrays()
    {
        var captured = new List<UpdateDefinition<MissionStatsEventsSampler>>();
        var capturedFilters = new List<Expression<Func<MissionStatsEventsSampler, bool>>>();

        _samplerContext
            .Setup(x => x.Upsert(It.IsAny<Expression<Func<MissionStatsEventsSampler, bool>>>(), It.IsAny<UpdateDefinition<MissionStatsEventsSampler>>()))
            .Callback<Expression<Func<MissionStatsEventsSampler, bool>>, UpdateDefinition<MissionStatsEventsSampler>>((f, u) =>
                {
                    capturedFilters.Add(f);
                    captured.Add(u);
                }
            )
            .Returns(Task.CompletedTask);

        var events = new List<BsonDocument>
        {
            SamplerEvent("u1", [1.5, -3, 2.5]),
            SamplerEvent("u1", [4.0, -1]),
            SamplerEvent("u2", [9.0])
        };

        await _subject.StoreAsync("session-1", events);

        captured.Should().HaveCount(2);

        var u1Update = captured[0];
        var u1Rendered = RenderSampler(u1Update);
        ExtractPushedDoubles(u1Rendered, "distanceOnFoot").Should().Equal(1.5, -3, 2.5, 4.0, -1);

        var u2Update = captured[1];
        var u2Rendered = RenderSampler(u2Update);
        ExtractPushedDoubles(u2Rendered, "distanceOnFoot").Should().Equal(9.0);

        u1Rendered["$setOnInsert"]["playerUid"].AsString.Should().Be("u1");
        u2Rendered["$setOnInsert"]["playerUid"].AsString.Should().Be("u2");
        u1Rendered["$setOnInsert"]["missionSessionId"].AsString.Should().Be("session-1");
    }

    [Fact]
    public async Task StoreAsync_CombatEvents_AppendToExistingBucket_WhenRoomAvailable()
    {
        var existing = new MissionStatsEventsCombat
        {
            Id = "bucket-1",
            MissionSessionId = "session-1",
            BucketIndex = 1,
            EventCount = 100,
            Events = []
        };
        _combatContext.Setup(x => x.Get(It.IsAny<Func<MissionStatsEventsCombat, bool>>())).Returns(new[] { existing });

        UpdateDefinition<MissionStatsEventsCombat> capturedUpdate = null;
        string capturedId = null;
        _combatContext.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MissionStatsEventsCombat>>()))
                      .Callback<string, UpdateDefinition<MissionStatsEventsCombat>>((id, u) =>
                          {
                              capturedId = id;
                              capturedUpdate = u;
                          }
                      )
                      .Returns(Task.CompletedTask);

        var events = new List<BsonDocument> { new() { { "type", "shot" }, { "uid", "u1" } }, new() { { "type", "shot" }, { "uid", "u1" } } };

        await _subject.StoreAsync("session-1", events);

        capturedId.Should().Be("bucket-1");
        capturedUpdate.Should().NotBeNull();
        var rendered = RenderCombat(capturedUpdate);
        ExtractPushedValues(rendered, "events").Should().HaveCount(2);
        rendered["$inc"]["eventCount"].AsInt32.Should().Be(2);

        _combatContext.Verify(x => x.Add(It.IsAny<MissionStatsEventsCombat>()), Times.Never);
    }

    [Fact]
    public async Task StoreAsync_CombatEvents_OpenNewBucketWhenCurrentFull()
    {
        var existing = new MissionStatsEventsCombat
        {
            Id = "bucket-1",
            MissionSessionId = "session-1",
            BucketIndex = 1,
            EventCount = MissionStatsEventsCombat.MaxEventsPerBucket,
            Events = []
        };
        _combatContext.Setup(x => x.Get(It.IsAny<Func<MissionStatsEventsCombat, bool>>())).Returns(new[] { existing });

        var added = new List<MissionStatsEventsCombat>();
        _combatContext.Setup(x => x.Add(It.IsAny<MissionStatsEventsCombat>()))
                      .Callback<MissionStatsEventsCombat>(b => added.Add(b))
                      .Returns(Task.CompletedTask);

        var events = new List<BsonDocument> { new() { { "type", "shot" } }, new() { { "type", "hit" } } };

        await _subject.StoreAsync("session-1", events);

        _combatContext.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<MissionStatsEventsCombat>>()), Times.Never);
        added.Should().HaveCount(1);
        added[0].BucketIndex.Should().Be(2);
        added[0].EventCount.Should().Be(2);
        added[0].MissionSessionId.Should().Be("session-1");
        added[0].Events.Should().HaveCount(2);
    }

    [Fact]
    public async Task StoreAsync_CombatEvents_OverflowSplitsIntoMultipleBuckets()
    {
        _combatContext.Setup(x => x.Get(It.IsAny<Func<MissionStatsEventsCombat, bool>>())).Returns(Array.Empty<MissionStatsEventsCombat>());

        var added = new List<MissionStatsEventsCombat>();
        _combatContext.Setup(x => x.Add(It.IsAny<MissionStatsEventsCombat>()))
                      .Callback<MissionStatsEventsCombat>(b => added.Add(
                                                              new MissionStatsEventsCombat
                                                              {
                                                                  MissionSessionId = b.MissionSessionId,
                                                                  BucketIndex = b.BucketIndex,
                                                                  EventCount = b.EventCount,
                                                                  Events = [..b.Events]
                                                              }
                                                          )
                      )
                      .Returns(Task.CompletedTask);

        var events = Enumerable.Range(0, MissionStatsEventsCombat.MaxEventsPerBucket + 1)
                               .Select(i => new BsonDocument { { "type", "shot" }, { "i", i } })
                               .ToList();

        await _subject.StoreAsync("session-1", events);

        added.Should().HaveCount(2);
        added[0].BucketIndex.Should().Be(1);
        added[0].EventCount.Should().Be(MissionStatsEventsCombat.MaxEventsPerBucket);
        added[1].BucketIndex.Should().Be(2);
        added[1].EventCount.Should().Be(1);
    }

    [Fact]
    public async Task StoreAsync_LifecycleEvents_AppendViaPushEach()
    {
        UpdateDefinition<MissionStatsEventsLifecycle> capturedUpdate = null;
        _lifecycleContext
            .Setup(x => x.Upsert(It.IsAny<Expression<Func<MissionStatsEventsLifecycle, bool>>>(), It.IsAny<UpdateDefinition<MissionStatsEventsLifecycle>>()))
            .Callback<Expression<Func<MissionStatsEventsLifecycle, bool>>, UpdateDefinition<MissionStatsEventsLifecycle>>((_, u) => capturedUpdate = u)
            .Returns(Task.CompletedTask);

        var events = new List<BsonDocument> { new() { { "type", "unconscious" }, { "uid", "u1" } }, new() { { "type", "explosivePlaced" }, { "uid", "u1" } } };

        await _subject.StoreAsync("session-1", events);

        _lifecycleContext.Verify(
            x => x.Upsert(It.IsAny<Expression<Func<MissionStatsEventsLifecycle, bool>>>(), It.IsAny<UpdateDefinition<MissionStatsEventsLifecycle>>()),
            Times.Once
        );

        capturedUpdate.Should().NotBeNull();
        var rendered = RenderLifecycle(capturedUpdate);
        rendered["$setOnInsert"].AsBsonDocument["missionSessionId"].AsString.Should().Be("session-1");
        ExtractPushedValues(rendered, "events").Should().HaveCount(2);
    }

    [Fact]
    public async Task StoreAsync_SamplerEventWithoutUid_IsSkipped()
    {
        var events = new List<BsonDocument> { new() { { "type", "samplerBatch" }, { "distanceOnFoot", new BsonArray(new[] { (BsonValue)1.0 }) } } };

        await _subject.StoreAsync("session-1", events);

        _samplerContext.Verify(
            x => x.Upsert(It.IsAny<Expression<Func<MissionStatsEventsSampler, bool>>>(), It.IsAny<UpdateDefinition<MissionStatsEventsSampler>>()),
            Times.Never
        );
    }

    [Fact]
    public async Task StoreAsync_CombatEvents_RetriesOnDuplicateKey_AfterConcurrentBucketInsert()
    {
        const string sessionId = "s-race";

        _combatContext.SetupSequence(x => x.Get(It.IsAny<Func<MissionStatsEventsCombat, bool>>()))
                      .Returns(Array.Empty<MissionStatsEventsCombat>())
                      .Returns(
                          new[]
                          {
                              new MissionStatsEventsCombat
                              {
                                  Id = "concurrent-bucket",
                                  MissionSessionId = sessionId,
                                  BucketIndex = 1,
                                  EventCount = 100
                              }
                          }
                      );

        var addedBuckets = new List<MissionStatsEventsCombat>();
        var firstAddCall = true;
        _combatContext.Setup(x => x.Add(It.IsAny<MissionStatsEventsCombat>()))
                      .Returns<MissionStatsEventsCombat>(b =>
                          {
                              if (firstAddCall)
                              {
                                  firstAddCall = false;
                                  throw MakeDuplicateKeyException();
                              }

                              addedBuckets.Add(b);
                              return Task.CompletedTask;
                          }
                      );

        var events = new List<BsonDocument> { BsonDocument.Parse("{\"type\":\"shot\",\"uid\":\"u1\"}") };

        await _subject.StoreAsync(sessionId, events);

        addedBuckets.Should().HaveCount(1);
        addedBuckets[0].BucketIndex.Should().Be(2);
    }

    private static MongoWriteException MakeDuplicateKeyException()
    {
        var ctor = typeof(WriteError).GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
            null,
            [typeof(ServerErrorCategory), typeof(int), typeof(string), typeof(BsonDocument)],
            null
        );
        var error = (WriteError)ctor.Invoke([ServerErrorCategory.DuplicateKey, 11000, "duplicate key", new BsonDocument()]);
        var connectionId = new MongoDB.Driver.Core.Connections.ConnectionId(
            new MongoDB.Driver.Core.Servers.ServerId(
                new MongoDB.Driver.Core.Clusters.ClusterId(),
                new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 27017)
            )
        );
        return new MongoWriteException(connectionId, error, null, null);
    }

    [Fact]
    public async Task StoreAsync_MixedBatch_RoutesEachTypeCorrectly()
    {
        _combatContext.Setup(x => x.Get(It.IsAny<Func<MissionStatsEventsCombat, bool>>())).Returns(Array.Empty<MissionStatsEventsCombat>());

        var events = new List<BsonDocument>
        {
            SamplerEvent("u1", [1.0]),
            new() { { "type", "shot" }, { "uid", "u1" } },
            new() { { "type", "unconscious" }, { "uid", "u1" } }
        };

        await _subject.StoreAsync("session-1", events);

        _samplerContext.Verify(
            x => x.Upsert(It.IsAny<Expression<Func<MissionStatsEventsSampler, bool>>>(), It.IsAny<UpdateDefinition<MissionStatsEventsSampler>>()),
            Times.Once
        );
        _combatContext.Verify(x => x.Add(It.IsAny<MissionStatsEventsCombat>()), Times.Once);
        _lifecycleContext.Verify(
            x => x.Upsert(It.IsAny<Expression<Func<MissionStatsEventsLifecycle, bool>>>(), It.IsAny<UpdateDefinition<MissionStatsEventsLifecycle>>()),
            Times.Once
        );
    }
}
