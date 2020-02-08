using FluentAssertions;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Services.Common;
using Xunit;

namespace UKSF.Tests.Unit.Services.Common {
    public class RankUtilitiesTests {
        [Fact]
        public void ShouldReturnCorrectSortValue() {
            Rank rank1 = new Rank {name = "Private", order = 1};
            Rank rank2 = new Rank {name = "Recruit", order = 0};

            int subject = rank1.Sort(rank2);

            subject.Should().Be(1);
        }
    }
}
