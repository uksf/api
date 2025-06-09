using FluentAssertions;
using UKSF.Api.ArmaMissions.Models;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Models;

public class MissionTests
{
    [Fact]
    public void ShouldSetFields()
    {
        Mission subject = new("TestData/testmission.Altis");

        subject.Path.Should().Be("TestData/testmission.Altis");
        subject.DescriptionPath.Should().Be("TestData/testmission.Altis/description.ext");
        subject.SqmPath.Should().Be("TestData/testmission.Altis/mission.sqm");
    }
}
