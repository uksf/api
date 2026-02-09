using System.Collections.Generic;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Models;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Models;

public class MissionPatchDataTests
{
    [Fact]
    public void ShouldInitializeWithDefaultValues()
    {
        var subject = new MissionPatchData();

        subject.OrderedUnits.Should().BeNull();
        subject.Players.Should().BeNull();
        subject.Ranks.Should().BeNull();
        subject.Units.Should().BeNull();
    }

    [Fact]
    public void Instance_ShouldAllowSettingStaticInstance()
    {
        var patchData = new MissionPatchData { Players = new List<MissionPlayer> { new() { Name = "TestPlayer" } } };

        MissionPatchData.Instance = patchData;

        MissionPatchData.Instance.Should().Be(patchData);
        MissionPatchData.Instance.Players.Should().HaveCount(1);
        MissionPatchData.Instance.Players[0].Name.Should().Be("TestPlayer");
    }
}
