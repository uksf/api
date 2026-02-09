using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Models;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Models;

public class MissionEntityItemTests
{
    [Fact]
    public void ShouldInitializeWithDefaultValues()
    {
        var subject = new MissionEntityItem();

        subject.DataType.Should().BeNull();
        subject.IsPlayable.Should().BeFalse();
        subject.MissionEntity.Should().BeNull();
        subject.Type.Should().BeNull();
        subject.RawMissionEntities.Should().NotBeNull().And.BeEmpty().And.BeOfType<List<string>>();
        subject.RawMissionEntityItem.Should().NotBeNull().And.BeEmpty().And.BeOfType<List<string>>();
    }
}
