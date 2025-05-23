using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services.Personnel;

public class RanksServiceTests
{
    private readonly Mock<IRanksContext> _mockRanksDataService;
    private readonly RanksService _ranksService;

    public RanksServiceTests()
    {
        _mockRanksDataService = new Mock<IRanksContext>();
        _ranksService = new RanksService(_mockRanksDataService.Object);
    }

    [Fact]
    public void ShouldGetCorrectIndex()
    {
        DomainRank rank1 = new() { Name = "Private" };
        DomainRank rank2 = new() { Name = "Recruit" };
        List<DomainRank> mockCollection = [rank1, rank2];

        _mockRanksDataService.Setup(x => x.Get()).Returns(mockCollection);
        _mockRanksDataService.Setup(x => x.GetSingle("Private")).Returns(rank1);

        var subject = _ranksService.GetRankOrder("Private");

        subject.Should().Be(0);
    }

    [Fact]
    public void ShouldGetCorrectSortValueByName()
    {
        DomainRank rank1 = new() { Name = "Private", Order = 0 };
        DomainRank rank2 = new() { Name = "Recruit", Order = 1 };
        List<DomainRank> mockCollection = [rank1, rank2];

        _mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => mockCollection.FirstOrDefault(y => y.Name == x));

        var subject = _ranksService.Sort("Recruit", "Private");

        subject.Should().Be(1);
    }

    [Fact]
    public void ShouldReturnEqualWhenBothNull()
    {
        _mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(null);

        var subject = _ranksService.IsEqual("Private", "Recruit");

        subject.Should().Be(true);
    }

    [Fact]
    public void ShouldReturnInvalidIndexGetIndexWhenRankNotFound()
    {
        _mockRanksDataService.Setup(x => x.Get()).Returns(new List<DomainRank>());

        var subject = _ranksService.GetRankOrder("Private");
        _mockRanksDataService.Setup(x => x.GetSingle("Private")).Returns<DomainRank>(null);

        subject.Should().Be(-1);
    }

    [Fact]
    public void ShouldReturnZeroForSortWhenRanksAreNull()
    {
        _mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(null);

        var subject = _ranksService.Sort("Recruit", "Private");

        subject.Should().Be(0);
    }

    [Fact]
    public void ShouldSortCollection()
    {
        DomainAccount account1 = new() { Rank = "Private" };
        DomainAccount account2 = new() { Rank = "Candidate" };
        DomainAccount account3 = new() { Rank = "Recruit" };
        DomainAccount account4 = new() { Rank = "Private" };
        List<DomainAccount> subject = [account1, account2, account3, account4];

        DomainRank rank1 = new() { Name = "Private", Order = 0 };
        DomainRank rank2 = new() { Name = "Recruit", Order = 1 };
        DomainRank rank3 = new() { Name = "Candidate", Order = 2 };
        List<DomainRank> mockCollection = [rank1, rank2, rank3];

        _mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => mockCollection.FirstOrDefault(y => y.Name == x));

        subject = subject.OrderBy(x => x.Rank, new RankComparer(_ranksService)).ToList();

        subject.Should().ContainInOrder(account1, account4, account3, account2);
    }

    [Theory]
    [InlineData("Private", "Recruit", true)]
    [InlineData("Recruit", "Private", false)]
    [InlineData("Corporal", "Private", false)]
    [InlineData("Sergeant", "Corporal", false)]
    public void ShouldResolveSuperior(string rankName1, string rankName2, bool expected)
    {
        DomainRank rank1 = new() { Name = "Private", Order = 0 };
        DomainRank rank2 = new() { Name = "Recruit", Order = 1 };
        DomainRank rank3 = new() { Name = "Candidate", Order = 2 };
        List<DomainRank> mockCollection = [rank1, rank2, rank3];

        _mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => mockCollection.FirstOrDefault(y => y.Name == x));

        var subject = _ranksService.IsSuperior(rankName1, rankName2);

        subject.Should().Be(expected);
    }

    [Theory]
    [InlineData("Private", "Private", true)]
    [InlineData("Recruit", "Private", false)]
    [InlineData("Corporal", "Private", false)]
    public void ShouldResolveEqual(string rankName1, string rankName2, bool expected)
    {
        DomainRank rank1 = new() { Name = "Private", Order = 0 };
        DomainRank rank2 = new() { Name = "Recruit", Order = 1 };
        DomainRank rank3 = new() { Name = "Candidate", Order = 2 };
        List<DomainRank> mockCollection = [rank1, rank2, rank3];

        _mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => mockCollection.FirstOrDefault(y => y.Name == x));

        var subject = _ranksService.IsEqual(rankName1, rankName2);

        subject.Should().Be(expected);
    }

    [Theory]
    [InlineData("Private", "Private", true)]
    [InlineData("Private", "Recruit", true)]
    [InlineData("Recruit", "Private", false)]
    [InlineData("Corporal", "Private", false)]
    public void ShouldResolveSuperiorOrEqual(string rankName1, string rankName2, bool expected)
    {
        DomainRank rank1 = new() { Name = "Private", Order = 0 };
        DomainRank rank2 = new() { Name = "Recruit", Order = 1 };
        DomainRank rank3 = new() { Name = "Candidate", Order = 2 };
        List<DomainRank> mockCollection = [rank1, rank2, rank3];

        _mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => mockCollection.FirstOrDefault(y => y.Name == x));

        var subject = _ranksService.IsSuperiorOrEqual(rankName1, rankName2);

        subject.Should().Be(expected);
    }
}
