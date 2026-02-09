using FluentAssertions;
using UKSF.Api.ArmaMissions.Models;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Models;

public class MissionPlayerTests
{
    [Fact]
    public void ShouldInitializeWithDefaultValues()
    {
        var subject = new MissionPlayer();

        subject.Account.Should().BeNull();
        subject.Name.Should().BeNull();
        subject.ObjectClass.Should().BeNull();
        subject.Rank.Should().BeNull();
        subject.Unit.Should().BeNull();
    }
}
