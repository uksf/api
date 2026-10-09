using System;
using System.Threading.Tasks;
using FluentAssertions;
using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Models;
using Xunit;

namespace UKSF.Api.Core.Tests.Context.Base;

public sealed class FindAndUpdateClaimTests : IDisposable
{
    private readonly MongoDbRunner _runner = MongoDbRunner.Start(singleNodeReplSet: false);
    private readonly ClaimContext _context;

    public FindAndUpdateClaimTests()
    {
        var database = new MongoClient(_runner.ConnectionString).GetDatabase($"claim-{ObjectId.GenerateNewId()}");
        database.GetCollection<Claimable>("claimables").InsertOne(new Claimable { Key = "session-1" });
        _context = new ClaimContext(new MongoCollectionFactory(database));
    }

    public void Dispose()
    {
        _runner.Dispose();
    }

    [Fact]
    public async Task FindAndUpdate_ReportsWhetherADocumentMatched_SoOnlyOneCallerWinsAClaim()
    {
        var first = await _context.FindAndUpdate(x => x.Key == "session-1" && x.ClaimedBy == null, Builders<Claimable>.Update.Set(x => x.ClaimedBy, "first"));
        var second = await _context.FindAndUpdate(x => x.Key == "session-1" && x.ClaimedBy == null, Builders<Claimable>.Update.Set(x => x.ClaimedBy, "second"));

        first.Should().BeTrue();
        second.Should().BeFalse();
        _context.FindFirst(x => x.Key == "session-1").ClaimedBy.Should().Be("first");
    }

    public class Claimable : MongoObject
    {
        public string Key { get; set; }
        public string ClaimedBy { get; set; }
    }

    private sealed class ClaimContext(IMongoCollectionFactory factory) : MongoContextBase<Claimable>(factory, "claimables");
}
