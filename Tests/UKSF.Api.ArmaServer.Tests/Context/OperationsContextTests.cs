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

public class OperationsContextTests
{
    private readonly Mock<IMongoCollectionFactory> _mockFactory = new();
    private readonly Mock<IMongoCollection<DomainOperation>> _mockDataCollection = new();
    private readonly OperationsContext _operationsContext;

    public OperationsContextTests()
    {
        Mock<IEventBus> mockEventBus = new();
        Mock<IVariablesService> mockVariablesService = new();
        _mockFactory.Setup(x => x.CreateMongoCollection<DomainOperation>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);
        _operationsContext = new OperationsContext(_mockFactory.Object, mockEventBus.Object, mockVariablesService.Object);
    }

    [Fact]
    public void Should_use_operations_collection()
    {
        _mockFactory.Verify(x => x.CreateMongoCollection<DomainOperation>("operations"), Times.Once);
    }

    [Fact]
    public void Should_return_operations_from_collection()
    {
        DomainOperation a = new() { Title = "Alpha" };
        DomainOperation b = new() { Title = "Bravo" };
        _mockDataCollection.Setup(x => x.Get()).Returns([a, b]);

        var subject = _operationsContext.Get();

        subject.Should().Contain([a, b]);
    }

    [Fact]
    public void CampaignId_is_stored_as_object_id()
    {
        var attribute = typeof(DomainOperation).GetProperty(nameof(DomainOperation.CampaignId))
                                                !.GetCustomAttribute<BsonRepresentationAttribute>();

        attribute.Should().NotBeNull();
        attribute!.Representation.Should().Be(BsonType.ObjectId);
    }

    [Fact]
    public void OperationStatus_round_trips_explicit_values()
    {
        ((int)OperationStatus.Upcoming).Should().Be(0);
        ((int)OperationStatus.Current).Should().Be(1);
        ((int)OperationStatus.Past).Should().Be(2);
        new DomainOperation().Status.Should().Be(OperationStatus.Upcoming);
    }

    [Fact]
    public void CampaignId_and_status_round_trip_through_bson()
    {
        var campaignId = ObjectId.GenerateNewId();
        DomainOperation operation = new()
        {
            CampaignId = campaignId.ToString(),
            Title = "Alpha",
            Brief = "Brief",
            Status = OperationStatus.Current
        };

        var bson = operation.ToBsonDocument();
        bson["campaignId"].BsonType.Should().Be(BsonType.ObjectId);
        bson["campaignId"].AsObjectId.Should().Be(campaignId);
        bson["status"].AsInt32.Should().Be(1);

        var roundTripped = BsonSerializer.Deserialize<DomainOperation>(bson);
        roundTripped.CampaignId.Should().Be(campaignId.ToString());
        roundTripped.Status.Should().Be(OperationStatus.Current);
        roundTripped.Title.Should().Be("Alpha");
        roundTripped.Brief.Should().Be("Brief");
    }

    [Theory]
    [InlineData(OperationStatus.Upcoming, 0)]
    [InlineData(OperationStatus.Current, 1)]
    [InlineData(OperationStatus.Past, 2)]
    public void OperationStatus_serializes_as_int_and_deserializes(OperationStatus status, int expected)
    {
        DomainOperation operation = new() { CampaignId = ObjectId.GenerateNewId().ToString(), Status = status };

        var bson = operation.ToBsonDocument();
        bson["status"].AsInt32.Should().Be(expected);

        BsonSerializer.Deserialize<DomainOperation>(bson).Status.Should().Be(status);
    }
}
