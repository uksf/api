using System.Reflection;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Context;

public class CampaignMissionsContextTests
{
    private readonly Mock<IMongoCollectionFactory> _mockFactory = new();
    private readonly Mock<IMongoCollection<DomainMission>> _mockDataCollection = new();
    private readonly CampaignMissionsContext _campaignMissionsContext;

    public CampaignMissionsContextTests()
    {
        Mock<IEventBus> mockEventBus = new();
        Mock<IVariablesService> mockVariablesService = new();
        _mockFactory.Setup(x => x.CreateMongoCollection<DomainMission>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);
        _campaignMissionsContext = new CampaignMissionsContext(_mockFactory.Object, mockEventBus.Object, mockVariablesService.Object);
    }

    [Fact]
    public void Should_use_campaignMissions_collection()
    {
        _mockFactory.Verify(x => x.CreateMongoCollection<DomainMission>("campaignMissions"), Times.Once);
    }

    [Fact]
    public void Should_return_missions_from_collection()
    {
        DomainMission a = new() { Title = "Alpha" };
        DomainMission b = new() { Title = "Bravo" };
        _mockDataCollection.Setup(x => x.Get()).Returns([a, b]);

        var subject = _campaignMissionsContext.Get();

        subject.Should().Contain([a, b]);
    }

    [Fact]
    public void OperationId_is_stored_as_object_id()
    {
        var attribute = typeof(DomainMission).GetProperty(nameof(DomainMission.OperationId))
                                             !.GetCustomAttribute<BsonRepresentationAttribute>();

        attribute.Should().NotBeNull();
        attribute!.Representation.Should().Be(BsonType.ObjectId);
    }

    [Fact]
    public void Mission_does_not_store_campaignId()
    {
        typeof(DomainMission).GetProperty("CampaignId").Should().BeNull();
    }

    [Fact]
    public void MissionStatus_round_trips_explicit_values()
    {
        ((int)MissionStatus.Scheduled).Should().Be(0);
        ((int)MissionStatus.Complete).Should().Be(1);
        new DomainMission().Status.Should().Be(MissionStatus.Scheduled);
    }

    [Fact]
    public void OperationId_and_status_round_trip_through_bson()
    {
        var operationId = ObjectId.GenerateNewId();
        var serverId = ObjectId.GenerateNewId();
        DomainMission mission = new()
        {
            OperationId = operationId.ToString(),
            Title = "Alpha",
            ServerId = serverId.ToString(),
            Status = MissionStatus.Complete,
            MissionName = ""
        };

        var bson = mission.ToBsonDocument();
        bson.Contains("campaignId").Should().BeFalse();
        bson["operationId"].BsonType.Should().Be(BsonType.ObjectId);
        bson["operationId"].AsObjectId.Should().Be(operationId);
        bson["serverId"].BsonType.Should().Be(BsonType.ObjectId);
        bson["status"].AsInt32.Should().Be(1);

        var roundTripped = BsonSerializer.Deserialize<DomainMission>(bson);
        roundTripped.OperationId.Should().Be(operationId.ToString());
        roundTripped.ServerId.Should().Be(serverId.ToString());
        roundTripped.Status.Should().Be(MissionStatus.Complete);
        typeof(DomainMission).GetProperty("CampaignId").Should().BeNull();
    }

    [Theory]
    [InlineData(MissionStatus.Scheduled, 0)]
    [InlineData(MissionStatus.Complete, 1)]
    public void MissionStatus_serializes_as_int_and_deserializes(MissionStatus status, int expected)
    {
        DomainMission mission = new() { OperationId = ObjectId.GenerateNewId().ToString(), Status = status };

        var bson = mission.ToBsonDocument();
        bson["status"].AsInt32.Should().Be(expected);

        BsonSerializer.Deserialize<DomainMission>(bson).Status.Should().Be(status);
    }
}
