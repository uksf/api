using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;
using Xunit;

namespace UKSF.Tests.Unit.Data.Personnel;

public class RanksDataServiceTests
{
    private readonly Mock<IMongoCollection<DomainRank>> _mockDataCollection;
    private readonly RanksContext _ranksContext;

    public RanksDataServiceTests()
    {
        Mock<IMongoCollectionFactory> mockDataCollectionFactory = new();
        Mock<IEventBus> mockEventBus = new();
        _mockDataCollection = new();

        mockDataCollectionFactory.Setup(x => x.CreateMongoCollection<DomainRank>(It.IsAny<string>())).Returns(_mockDataCollection.Object);

        _ranksContext = new(mockDataCollectionFactory.Object, mockEventBus.Object);
    }

    [Fact]
    public void Should_return_collection_in_order()
    {
        DomainRank rank1 = new() { Order = 2 };
        DomainRank rank2 = new() { Order = 0 };
        DomainRank rank3 = new() { Order = 1 };

        _mockDataCollection.Setup(x => x.Get()).Returns(new List<DomainRank> { rank1, rank2, rank3 });

        var subject = _ranksContext.Get();

        subject.Should().ContainInOrder(rank2, rank3, rank1);
    }

    [Fact]
    public void Should_return_item_by_name()
    {
        DomainRank rank1 = new() { Name = "Private", Order = 2 };
        DomainRank rank2 = new() { Name = "Recruit", Order = 1 };
        DomainRank rank3 = new() { Name = "Candidate", Order = 0 };

        _mockDataCollection.Setup(x => x.Get()).Returns(new List<DomainRank> { rank1, rank2, rank3 });

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
