using System;
using System.Threading.Tasks;
using FluentAssertions;
using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Controllers;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Mappers;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Tests.Controllers;

public sealed class VerifyCommsControllerTests : IDisposable
{
    private readonly MongoDbRunner _runner = MongoDbRunner.Start(singleNodeReplSet: false);
    private readonly MongoDB.Driver.IMongoCollection<DomainAccount> _accounts;
    private readonly AccountContext _accountContext;
    private readonly string _callerId = ObjectId.GenerateNewId().ToString();
    private readonly string _otherId = ObjectId.GenerateNewId().ToString();

    public VerifyCommsControllerTests()
    {
        var database = new MongoClient(_runner.ConnectionString).GetDatabase($"verifycomms-{ObjectId.GenerateNewId()}");
        _accounts = database.GetCollection<DomainAccount>("accounts");
        _accounts.InsertMany([new DomainAccount { Id = _callerId, Email = "caller@verify" }, new DomainAccount { Id = _otherId, Email = "other@verify" }]);

        Mock<IVariablesService> variables = new();
        variables.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);
        _accountContext = new AccountContext(new MongoCollectionFactory(database), new Mock<IEventBus>().Object, variables.Object);
        _accountContext.Get();
    }

    public void Dispose()
    {
        _runner.Dispose();
    }

    private VerifyCommsController Controller(bool verifyMode)
    {
        Mock<IHttpContextService> httpContext = new();
        httpContext.Setup(x => x.GetUserId()).Returns(_callerId);
        var mapper = new AccountMapper(new Mock<IDisplayNameService>().Object);
        return new VerifyCommsController(_accountContext, httpContext.Object, mapper, new VerifyMode(verifyMode ? "1" : null, null));
    }

    private DomainAccount Stored(string id)
    {
        return _accounts.Find(x => x.Id == id).Single();
    }

    [Fact]
    public async Task WhenVerifyModeIsOff_Returns404AndChangesNothing()
    {
        var act = () => Controller(false).SeedComms();

        (await act.Should().ThrowAsync<NotFoundException>()).Which.StatusCode.Should().Be(404);
        Stored(_callerId).Steamname.Should().BeNull();
        Stored(_callerId).DiscordId.Should().BeNull();
        Stored(_callerId).TeamspeakIdentities.Should().BeNull();
        _accountContext.GetSingle(_callerId).Steamname.Should().BeNull();
    }

    [Fact]
    public async Task WhenVerifyModeIsOn_SeedsTheCallersAccountInMongoAndTheCache()
    {
        var response = await Controller(true).SeedComms();

        var stored = Stored(_callerId);
        stored.TeamspeakIdentities.Should().BeEquivalentTo([-1]);
        stored.Steamname.Should().Be($"verify-{_callerId}");
        stored.DiscordId.Should().Be($"verify-{_callerId}");

        var cached = _accountContext.GetSingle(_callerId);
        cached.TeamspeakIdentities.Should().BeEquivalentTo([-1]);
        cached.Steamname.Should().Be($"verify-{_callerId}");
        cached.DiscordId.Should().Be($"verify-{_callerId}");

        response.Id.Should().Be(_callerId);
        response.TeamspeakIdentities.Should().BeEquivalentTo([-1]);
        response.Steamname.Should().Be($"verify-{_callerId}");
        response.DiscordId.Should().Be($"verify-{_callerId}");
    }

    [Fact]
    public async Task WhenVerifyModeIsOn_LeavesOtherAccountsUntouched()
    {
        await Controller(true).SeedComms();

        var other = Stored(_otherId);
        other.TeamspeakIdentities.Should().BeNull();
        other.Steamname.Should().BeNull();
        other.DiscordId.Should().BeNull();
        _accountContext.GetSingle(_otherId).Steamname.Should().BeNull();
    }
}
