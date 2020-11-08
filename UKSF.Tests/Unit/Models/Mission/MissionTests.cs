using FluentAssertions;
using Xunit;

namespace UKSF.Tests.Unit.Models.Mission {
    public class MissionTests {
        [Fact]
        public void ShouldSetFields() {
            Api.ArmaMissions.Models.Mission subject = new Api.ArmaMissions.Models.Mission("testdata/testmission.Altis");

            subject.path.Should().Be("testdata/testmission.Altis");
            subject.descriptionPath.Should().Be("testdata/testmission.Altis/description.ext");
            subject.sqmPath.Should().Be("testdata/testmission.Altis/mission.sqm");
        }
    }
}
