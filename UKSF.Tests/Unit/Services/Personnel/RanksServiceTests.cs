using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using Xunit;

namespace UKSF.Tests.Unit.Services.Personnel
{
    public class RanksServiceTests
    {
        private readonly Mock<IRanksContext> _mockRanksDataService;
        private readonly RanksService _ranksService;

        public RanksServiceTests()
        {
            _mockRanksDataService = new();
            _ranksService = new(_mockRanksDataService.Object);
        }

        [Fact]
        public void ShouldGetCorrectIndex()
        {
            Rank rank1 = new() { Name = "Private" };
            Rank rank2 = new() { Name = "Recruit" };
            List<Rank> mockCollection = new() { rank1, rank2 };

            _mockRanksDataService.Setup(x => x.Get()).Returns(mockCollection);
            _mockRanksDataService.Setup(x => x.GetSingle("Private")).Returns(rank1);

            int subject = _ranksService.GetRankOrder("Private");

            subject.Should().Be(0);
        }

        [Fact]
        public void ShouldGetCorrectSortValueByName()
        {
            Rank rank1 = new() { Name = "Private", Order = 0 };
            Rank rank2 = new() { Name = "Recruit", Order = 1 };
            List<Rank> mockCollection = new() { rank1, rank2 };

            _mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => mockCollection.FirstOrDefault(y => y.Name == x));

            int subject = _ranksService.Sort("Recruit", "Private");

            subject.Should().Be(1);
        }

        [Fact]
        public void ShouldReturnEqualWhenBothNull()
        {
            _mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(null);

            bool subject = _ranksService.IsEqual("Private", "Recruit");

            subject.Should().Be(true);
        }

        [Fact]
        public void ShouldReturnInvalidIndexGetIndexWhenRankNotFound()
        {
            _mockRanksDataService.Setup(x => x.Get()).Returns(new List<Rank>());

            int subject = _ranksService.GetRankOrder("Private");
            _mockRanksDataService.Setup(x => x.GetSingle("Private")).Returns<Rank>(null);

            subject.Should().Be(-1);
        }

        [Fact]
        public void ShouldReturnZeroForSortWhenRanksAreNull()
        {
            _mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(null);

            int subject = _ranksService.Sort("Recruit", "Private");

            subject.Should().Be(0);
        }

        [Fact]
        public void ShouldSortCollection()
        {
            DomainAccount account1 = new() { Rank = "Private" };
            DomainAccount account2 = new() { Rank = "Candidate" };
            DomainAccount account3 = new() { Rank = "Recruit" };
            DomainAccount account4 = new() { Rank = "Private" };
            List<DomainAccount> subject = new() { account1, account2, account3, account4 };

            Rank rank1 = new() { Name = "Private", Order = 0 };
            Rank rank2 = new() { Name = "Recruit", Order = 1 };
            Rank rank3 = new() { Name = "Candidate", Order = 2 };
            List<Rank> mockCollection = new() { rank1, rank2, rank3 };

            _mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => mockCollection.FirstOrDefault(y => y.Name == x));

            subject = subject.OrderBy(x => x.Rank, new RankComparer(_ranksService)).ToList();

            subject.Should().ContainInOrder(account1, account4, account3, account2);
        }

        [Theory, InlineData("Private", "Recruit", true), InlineData("Recruit", "Private", false), InlineData("Corporal", "Private", false), InlineData("Sergeant", "Corporal", false)]
        public void ShouldResolveSuperior(string rankName1, string rankName2, bool expected)
        {
            Rank rank1 = new() { Name = "Private", Order = 0 };
            Rank rank2 = new() { Name = "Recruit", Order = 1 };
            Rank rank3 = new() { Name = "Candidate", Order = 2 };
            List<Rank> mockCollection = new() { rank1, rank2, rank3 };

            _mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => mockCollection.FirstOrDefault(y => y.Name == x));

            bool subject = _ranksService.IsSuperior(rankName1, rankName2);

            subject.Should().Be(expected);
        }

        [Theory, InlineData("Private", "Private", true), InlineData("Recruit", "Private", false), InlineData("Corporal", "Private", false)]
        public void ShouldResolveEqual(string rankName1, string rankName2, bool expected)
        {
            Rank rank1 = new() { Name = "Private", Order = 0 };
            Rank rank2 = new() { Name = "Recruit", Order = 1 };
            Rank rank3 = new() { Name = "Candidate", Order = 2 };
            List<Rank> mockCollection = new() { rank1, rank2, rank3 };

            _mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => mockCollection.FirstOrDefault(y => y.Name == x));

            bool subject = _ranksService.IsEqual(rankName1, rankName2);

            subject.Should().Be(expected);
        }

        [Theory, InlineData("Private", "Private", true), InlineData("Private", "Recruit", true), InlineData("Recruit", "Private", false), InlineData("Corporal", "Private", false)]
        public void ShouldResolveSuperiorOrEqual(string rankName1, string rankName2, bool expected)
        {
            Rank rank1 = new() { Name = "Private", Order = 0 };
            Rank rank2 = new() { Name = "Recruit", Order = 1 };
            Rank rank3 = new() { Name = "Candidate", Order = 2 };
            List<Rank> mockCollection = new() { rank1, rank2, rank3 };

            _mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => mockCollection.FirstOrDefault(y => y.Name == x));

            bool subject = _ranksService.IsSuperiorOrEqual(rankName1, rankName2);

            subject.Should().Be(expected);
        }
    }
}
