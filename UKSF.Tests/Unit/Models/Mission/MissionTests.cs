using FluentAssertions;
using Xunit;

namespace UKSF.Tests.Unit.Models.Mission {
    public class MissionTests {
        [Fact]
        public void ShouldSetFields() {
            Api.Models.Mission.Mission subject = new Api.Models.Mission.Mission("testdata/testmission.Altis");

            subject.path.Should().Be("testdata/testmission.Altis");
            subject.descriptionPath.Should().Be("testdata/testmission.Altis/description.ext");
            subject.sqmPath.Should().Be("testdata/testmission.Altis/mission.sqm");
        }
    }
}
