using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Models;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Models;

public class MissionEntityTests
{
    [Fact]
    public void ShouldInitializeWithDefaultValues()
    {
        var subject = new MissionEntity();

        subject.ItemsCount.Should().Be(0);
        subject.MissionEntityItems.Should().NotBeNull().And.BeEmpty().And.BeOfType<List<MissionEntityItem>>();
    }
}
