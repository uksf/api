using FluentAssertions;
using Xunit;

namespace UKSF.Tests.Unit.Models.Mission;

public class MissionTests
{
    [Fact]
    public void ShouldSetFields()
    {
        Api.ArmaMissions.Models.Mission subject = new("testdata/testmission.Altis");

        subject.Path.Should().Be("testdata/testmission.Altis");
        subject.DescriptionPath.Should().Be("testdata/testmission.Altis/description.ext");
        subject.SqmPath.Should().Be("testdata/testmission.Altis/mission.sqm");
    }
}
