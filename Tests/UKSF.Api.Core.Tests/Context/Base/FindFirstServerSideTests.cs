using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using FluentAssertions;
using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Context.Base;

public sealed class FindFirstServerSideTests : IDisposable
{
    private const string Collection = "findFirstBuckets";
    private readonly MongoDbRunner _runner = MongoDbRunner.Start(singleNodeReplSet: false);
    private readonly IMongoDatabase _database;
    private readonly BucketContext _context;

    public FindFirstServerSideTests()
    {
        _database = new MongoClient(_runner.ConnectionString).GetDatabase($"findfirst-{ObjectId.GenerateNewId()}");
        MongoDB.Driver.IMongoCollection<Bucket> buckets = _database.GetCollection<Bucket>(Collection);
        buckets.Indexes.CreateOne(new CreateIndexModel<Bucket>(Builders<Bucket>.IndexKeys.Ascending(x => x.SessionId).Ascending(x => x.BucketIndex)));
        buckets.InsertMany(
            Enumerable.Range(0, 50).SelectMany(session => Enumerable.Range(0, 4).Select(index => new Bucket { SessionId = $"session-{session}", BucketIndex = index }))
        );
        _context = new BucketContext(new MongoCollectionFactory(_database));
    }

    public void Dispose()
    {
        _runner.Dispose();
    }

    private List<BsonDocument> ReadProfiles()
    {
        return _database.GetCollection<BsonDocument>("system.profile")
                        .Find(Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("ns", $"{_database.DatabaseNamespace.DatabaseName}.{Collection}"), Builders<BsonDocument>.Filter.Exists("docsExamined")))
                        .ToList();
    }

    private int DocumentsExamined()
    {
        return ReadProfiles().Sum(x => x["docsExamined"].ToInt32());
    }

    private void StartProfiling()
    {
        _database.RunCommand<BsonDocument>(new BsonDocument("profile", 2));
    }

    [Fact]
    public void FindFirst_ReturnsTheMatchingDocument()
    {
        var bucket = _context.FindFirst(x => x.SessionId == "session-31" && x.BucketIndex == 2);

        bucket.SessionId.Should().Be("session-31");
        bucket.BucketIndex.Should().Be(2);
    }

    [Fact]
    public void FindFirst_WithADescendingSort_ReturnsTheHighestBucket()
    {
        _context.FindFirst(x => x.SessionId == "session-7", x => x.BucketIndex).BucketIndex.Should().Be(3);
    }

    [Fact]
    public void FindFirst_WithNoMatch_ReturnsNull()
    {
        _context.FindFirst(x => x.SessionId == "missing").Should().BeNull();
    }

    [Fact]
    public void FindFirst_FiltersAndSortsInMongoThroughTheIndex()
    {
        StartProfiling();

        _context.FindFirst(x => x.SessionId == "session-42", x => x.BucketIndex);

        DocumentsExamined().Should().Be(1);
        ReadProfiles().Should().ContainSingle().Which["planSummary"].AsString.Should().StartWith("IXSCAN");
    }

    [Fact]
    public void GetSingleWithADelegate_StreamsTheCollection()
    {
        StartProfiling();

        _context.GetSingle(x => x.SessionId == "session-42");

        DocumentsExamined().Should().BeGreaterThan(160);
    }

    [Fact]
    public void FindFirst_OnACachedContext_UsesTheCacheWithoutQueryingMongo()
    {
        Mock<IMongoCollectionFactory> factory = new();
        Mock<UKSF.Api.Core.Context.Base.IMongoCollection<Bucket>> collection = new();
        factory.Setup(x => x.CreateMongoCollection<Bucket>(It.IsAny<string>())).Returns(collection.Object);
        collection.Setup(x => x.Get()).Returns(new List<Bucket> { new() { SessionId = "a", BucketIndex = 1 }, new() { SessionId = "a", BucketIndex = 5 } });
        Mock<IVariablesService> variables = new();
        variables.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);
        CachedBucketContext cached = new(factory.Object, new EventBus(), variables.Object);

        cached.FindFirst(x => x.SessionId == "a", x => x.BucketIndex).BucketIndex.Should().Be(5);

        collection.Verify(x => x.FindFirst(It.IsAny<Expression<Func<Bucket, bool>>>(), It.IsAny<Expression<Func<Bucket, object>>>()), Times.Never);
    }

    private (CachedBucketContext Context, Mock<UKSF.Api.Core.Context.Base.IMongoCollection<Bucket>> Collection) CachedContext(bool cacheEnabled)
    {
        Mock<IMongoCollectionFactory> factory = new();
        Mock<UKSF.Api.Core.Context.Base.IMongoCollection<Bucket>> collection = new();
        factory.Setup(x => x.CreateMongoCollection<Bucket>(It.IsAny<string>())).Returns(collection.Object);
        collection.Setup(x => x.Get()).Returns(new List<Bucket> { new() { SessionId = "a", BucketIndex = 1 }, new() { SessionId = "a", BucketIndex = 5 } });
        Mock<IVariablesService> variables = new();
        variables.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(cacheEnabled);
        return (new CachedBucketContext(factory.Object, new EventBus(), variables.Object), collection);
    }

    [Fact]
    public void FindFirst_OnACachedContextWithoutASort_ReturnsTheFirstCachedMatch()
    {
        CachedContext(cacheEnabled: true).Context.FindFirst(x => x.SessionId == "a").BucketIndex.Should().Be(1);
    }

    [Fact]
    public void FindFirst_OnACachedContextWithNoMatch_ReturnsNull()
    {
        CachedContext(cacheEnabled: true).Context.FindFirst(x => x.SessionId == "missing").Should().BeNull();
    }

    [Fact]
    public void FindFirst_OnACachedContextWithTheCacheOff_QueriesMongo()
    {
        var (context, collection) = CachedContext(cacheEnabled: false);
        collection.Setup(x => x.FindFirst(It.IsAny<Expression<Func<Bucket, bool>>>(), It.IsAny<Expression<Func<Bucket, object>>>()))
                  .Returns(new Bucket { SessionId = "from-mongo", BucketIndex = 9 });

        context.FindFirst(x => x.SessionId == "a").SessionId.Should().Be("from-mongo");
    }

    public class Bucket : MongoObject
    {
        public string SessionId { get; set; }
        public int BucketIndex { get; set; }
    }

    private sealed class BucketContext(IMongoCollectionFactory factory) : MongoContextBase<Bucket>(factory, Collection);

    private sealed class CachedBucketContext(IMongoCollectionFactory factory, IEventBus eventBus, IVariablesService variables)
        : CachedMongoContext<Bucket>(factory, eventBus, variables, Collection);
}
