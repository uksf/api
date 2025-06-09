using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Context.Personnel;

public class RanksContextTests
{
    private readonly Mock<IMongoCollection<DomainRank>> _mockDataCollection;
    private readonly RanksContext _ranksContext;

    public RanksContextTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        Mock<IVariablesService> mockVariablesService = new();
        _mockDataCollection = new Mock<IMongoCollection<DomainRank>>();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainRank>(It.IsAny<string>())).Returns(_mockDataCollection.Object);
        mockVariablesService.Setup(x => x.GetFeatureState("USE_MEMORY_DATA_CACHE")).Returns(true);

        _ranksContext = new RanksContext(mockDataCollectionFactory.Object, mockEventBus.Object, mockVariablesService.Object);
    }

    [Fact]
    public void Should_return_collection_in_order()
    {
        DomainRank rank1 = new() { Order = 2 };
        DomainRank rank2 = new() { Order = 0 };
        DomainRank rank3 = new() { Order = 1 };

        _mockDataCollection.Setup(x => x.Get())
        .Returns(
            new List<DomainRank>
            {
                rank1,
                rank2,
                rank3
            }
        );

        var subject = _ranksContext.Get();

        subject.Should().ContainInOrder(rank2, rank3, rank1);
    }

    [Fact]
    public void Should_return_item_by_name()
    {
        DomainRank rank1 = new() { Name = "Private", Order = 2 };
        DomainRank rank2 = new() { Name = "Recruit", Order = 1 };
        DomainRank rank3 = new() { Name = "Candidate", Order = 0 };

        _mockDataCollection.Setup(x => x.Get())
        .Returns(
            new List<DomainRank>
            {
                rank1,
                rank2,
                rank3
            }
        );

        var subject = _ranksContext.GetSingle("Recruit");

        subject.Should().Be(rank2);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Should_return_nothing_for_empty_or_null_name(string name)
    {
        _mockDataCollection.Setup(x => x.Get()).Returns(new List<DomainRank>());

        var subject = _ranksContext.GetSingle(name);

        subject.Should().Be(null);
    }
}
