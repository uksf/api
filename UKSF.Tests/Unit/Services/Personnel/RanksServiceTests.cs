using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Services.Personnel;
using Xunit;

namespace UKSF.Tests.Unit.Services.Personnel {
    public class RanksServiceTests {
        private readonly Mock<IRanksDataService> mockRanksDataService;
        private readonly RanksService ranksService;

        public RanksServiceTests() {
            mockRanksDataService = new Mock<IRanksDataService>();
            ranksService = new RanksService(mockRanksDataService.Object);
        }

        [Fact]
        public void ShouldGetCorrectIndex() {
            Rank rank1 = new Rank {name = "Private"};
            Rank rank2 = new Rank {name = "Recruit"};
            List<Rank> mockCollection = new List<Rank> {rank1, rank2};

            mockRanksDataService.Setup(x => x.Get()).Returns(mockCollection);

            int subject = ranksService.GetRankOrder("Private");

            subject.Should().Be(0);
        }

        [Fact]
        public void ShouldReturnInvalidIndexGetIndexWhenRankNotFound() {
            mockRanksDataService.Setup(x => x.Get()).Returns(new List<Rank>());

            int subject = ranksService.GetRankOrder("Private");

            subject.Should().Be(-1);
        }

        [Fact]
        public void ShouldGetCorrectSortValueByName() {
            Rank rank1 = new Rank {name = "Private", order = 0};
            Rank rank2 = new Rank {name = "Recruit", order = 1};
            List<Rank> mockCollection = new List<Rank> {rank1, rank2};

            mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => mockCollection.FirstOrDefault(y => y.name == x));

            int subject = ranksService.Sort("Recruit", "Private");

            subject.Should().Be(1);
        }

        [Fact]
        public void ShouldReturnZeroForSortWhenRanksAreNull() {
            mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(null);

            int subject = ranksService.Sort("Recruit", "Private");

            subject.Should().Be(0);
        }

        [Theory, InlineData("Private", "Recruit", true), InlineData("Recruit", "Private", false), InlineData("Corporal", "Private", false), InlineData("Sergeant", "Corporal", false)]
        public void ShouldResolveSuperior(string rankName1, string rankName2, bool expected) {
            Rank rank1 = new Rank {name = "Private", order = 0};
            Rank rank2 = new Rank {name = "Recruit", order = 1};
            Rank rank3 = new Rank {name = "Candidate", order = 2};
            List<Rank> mockCollection = new List<Rank> {rank1, rank2, rank3};

            mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => mockCollection.FirstOrDefault(y => y.name == x));

            bool subject = ranksService.IsSuperior(rankName1, rankName2);

            subject.Should().Be(expected);
        }

        [Theory, InlineData("Private", "Private", true), InlineData("Recruit", "Private", false), InlineData("Corporal", "Private", false)]
        public void ShouldResolveEqual(string rankName1, string rankName2, bool expected) {
            Rank rank1 = new Rank {name = "Private", order = 0};
            Rank rank2 = new Rank {name = "Recruit", order = 1};
            Rank rank3 = new Rank {name = "Candidate", order = 2};
            List<Rank> mockCollection = new List<Rank> {rank1, rank2, rank3};

            mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => mockCollection.FirstOrDefault(y => y.name == x));

            bool subject = ranksService.IsEqual(rankName1, rankName2);

            subject.Should().Be(expected);
        }

        [Fact]
        public void ShouldReturnEqualWhenBothNull() {
            mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(null);

            bool subject = ranksService.IsEqual("Private", "Recruit");

            subject.Should().Be(true);
        }


        [Theory, InlineData("Private", "Private", true), InlineData("Private", "Recruit", true), InlineData("Recruit", "Private", false), InlineData("Corporal", "Private", false)]
        public void ShouldResolveSuperiorOrEqual(string rankName1, string rankName2, bool expected) {
            Rank rank1 = new Rank {name = "Private", order = 0};
            Rank rank2 = new Rank {name = "Recruit", order = 1};
            Rank rank3 = new Rank {name = "Candidate", order = 2};
            List<Rank> mockCollection = new List<Rank> {rank1, rank2, rank3};

            mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => mockCollection.FirstOrDefault(y => y.name == x));

            bool subject = ranksService.IsSuperiorOrEqual(rankName1, rankName2);

            subject.Should().Be(expected);
        }

        [Fact]
        public void ShouldSortCollection() {
            Account account1 = new Account {rank = "Private"};
            Account account2 = new Account {rank = "Candidate"};
            Account account3 = new Account {rank = "Recruit"};
            Account account4 = new Account {rank = "Private"};
            List<Account> subject = new List<Account> {account1, account2, account3, account4};

            Rank rank1 = new Rank {name = "Private", order = 0};
            Rank rank2 = new Rank {name = "Recruit", order = 1};
            Rank rank3 = new Rank {name = "Candidate", order = 2};
            List<Rank> mockCollection = new List<Rank> {rank1, rank2, rank3};

            mockRanksDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => mockCollection.FirstOrDefault(y => y.name == x));

            subject = subject.OrderBy(x => x.rank, new RankComparer(ranksService)).ToList();

            subject.Should().ContainInOrder(account1, account4, account3, account2);
        }
    }
}
