using System;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class MigrationRemoveOperationCollectionsTests : IDisposable
{
    private readonly MongoDbRunner _runner = MongoDbRunner.Start(singleNodeReplSet: false);
    private readonly IMongoDatabase _database;
    private readonly MigrationUtility _sut;

    public MigrationRemoveOperationCollectionsTests()
    {
        _database = new MongoClient(_runner.ConnectionString).GetDatabase("test");
        _sut = new MigrationUtility(new Mock<IMigrationContext>().Object, _database, new Mock<IUksfLogger>().Object);
    }

    public void Dispose()
    {
        _runner.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Drops_Both_Collections_When_Present()
    {
        await _database.GetCollection<BsonDocument>("opord").InsertOneAsync(new BsonDocument { ["_id"] = ObjectId.GenerateNewId() });
        await _database.GetCollection<BsonDocument>("oprep").InsertOneAsync(new BsonDocument { ["_id"] = ObjectId.GenerateNewId() });

        await InvokeMigrateRemoveOperationCollections();

        var collections = await _database.ListCollectionNames().ToListAsync();
        collections.Should().NotContain("opord");
        collections.Should().NotContain("oprep");
    }

    [Fact]
    public async Task Does_Not_Throw_When_Collections_Absent()
    {
        var act = async () => await InvokeMigrateRemoveOperationCollections();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Drops_Opord_Even_When_Oprep_Absent()
    {
        await _database.GetCollection<BsonDocument>("opord").InsertOneAsync(new BsonDocument { ["_id"] = ObjectId.GenerateNewId() });

        await InvokeMigrateRemoveOperationCollections();

        var collections = await _database.ListCollectionNames().ToListAsync();
        collections.Should().NotContain("opord");
    }

    [Fact]
    public async Task Drops_Oprep_Even_When_Opord_Absent()
    {
        await _database.GetCollection<BsonDocument>("oprep").InsertOneAsync(new BsonDocument { ["_id"] = ObjectId.GenerateNewId() });

        await InvokeMigrateRemoveOperationCollections();

        var collections = await _database.ListCollectionNames().ToListAsync();
        collections.Should().NotContain("oprep");
    }

    private async Task InvokeMigrateRemoveOperationCollections()
    {
        var method = typeof(MigrationUtility).GetMethod("MigrateRemoveOperationCollections", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull("the migration step must be implemented");
        await (Task)method!.Invoke(_sut, [])!;
    }
}
