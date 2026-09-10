using System;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Context;

public class IntelPagesContextTests
{
    private readonly Mock<IMongoCollection<DomainIntelPage>> _mockDataCollection = new();
    private readonly IntelPagesContext _intelPagesContext;

    public IntelPagesContextTests()
    {
        Mock<IMongoCollectionFactory> mockFactory = new();
        Mock<IEventBus> mockEventBus = new();
        Mock<IVariablesService> mockVariablesService = new();
        mockFactory.Setup(x => x.CreateMongoCollection<DomainIntelPage>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);
        _intelPagesContext = new IntelPagesContext(mockFactory.Object, mockEventBus.Object, mockVariablesService.Object);
    }

    [Fact]
    public void Should_return_intel_pages_from_collection()
    {
        DomainIntelPage a = new() { Title = "Enemy Forces" };
        DomainIntelPage b = new() { Title = "Area of Operations" };
        _mockDataCollection.Setup(x => x.Get()).Returns([a, b]);

        var subject = _intelPagesContext.Get();

        subject.Should().Contain([a, b]);
    }

    [Fact]
    public void IntelScope_uses_explicit_campaign_operation_mission_values()
    {
        ((int)IntelScope.Campaign).Should().Be(0);
        ((int)IntelScope.Operation).Should().Be(1);
        ((int)IntelScope.Mission).Should().Be(2);
        Enum.GetNames<IntelScope>().Should().Equal("Campaign", "Operation", "Mission");
    }

    [Fact]
    public void OwnerId_and_scope_round_trip_through_bson()
    {
        var ownerId = ObjectId.GenerateNewId();
        DomainIntelPage page = new()
        {
            Scope = IntelScope.Mission,
            OwnerId = ownerId.ToString(),
            Title = "Forces",
            Body = "Body"
        };

        var bson = page.ToBsonDocument();
        bson["ownerId"].BsonType.Should().Be(BsonType.ObjectId);
        bson["ownerId"].AsObjectId.Should().Be(ownerId);
        bson["scope"].AsInt32.Should().Be(2);

        var roundTripped = BsonSerializer.Deserialize<DomainIntelPage>(bson);
        roundTripped.OwnerId.Should().Be(ownerId.ToString());
        roundTripped.Scope.Should().Be(IntelScope.Mission);
        roundTripped.Title.Should().Be("Forces");
    }

    [Theory]
    [InlineData(IntelScope.Campaign, 0)]
    [InlineData(IntelScope.Operation, 1)]
    [InlineData(IntelScope.Mission, 2)]
    public void IntelScope_serializes_as_int_and_deserializes(IntelScope scope, int expected)
    {
        DomainIntelPage page = new() { Scope = scope, OwnerId = ObjectId.GenerateNewId().ToString() };

        var bson = page.ToBsonDocument();
        bson["scope"].AsInt32.Should().Be(expected);

        BsonSerializer.Deserialize<DomainIntelPage>(bson).Scope.Should().Be(scope);
    }
}
