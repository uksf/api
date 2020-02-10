using FluentAssertions;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Services.Common;
using Xunit;

namespace UKSF.Tests.Unit.Services.Common {
    public class RankUtilitiesTests {
        [Fact]
        public void ShouldReturnCorrectSortValue() {
            Rank rank1 = new Rank {name = "Private", order = 0};
            Rank rank2 = new Rank {name = "Recruit", order = 1};

            int subject = RankUtilities.Sort(rank1, rank2);

            subject.Should().Be(-1);
        }

        [Fact]
        public void ShouldSortNullRankFirst() {
            Rank rank1 = new Rank {name = "Private", order = 1};

            int subject = RankUtilities.Sort(null, rank1);

            subject.Should().Be(1);
        }

        [Fact]
        public void ShouldSortNullRankSecond() {
            Rank rank1 = new Rank {name = "Private", order = 1};

            int subject = RankUtilities.Sort(rank1, null);

            subject.Should().Be(-1);
        }

        [Fact]
        public void ShouldSortNullRanks() {
            int subject = RankUtilities.Sort(null, null);

            subject.Should().Be(0);
        }
    }
}
