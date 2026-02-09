using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Models;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Models;

public class MissionUnitTests
{
    [Fact]
    public void ShouldInitializeWithDefaultValues()
    {
        var subject = new MissionUnit();

        subject.Callsign.Should().BeNull();
        subject.SourceUnit.Should().BeNull();
        subject.Members.Should().NotBeNull().And.BeEmpty().And.BeOfType<List<MissionPlayer>>();
        subject.Roles.Should().NotBeNull().And.BeEmpty().And.BeOfType<Dictionary<string, MissionPlayer>>();
    }
}
