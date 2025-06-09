using FluentAssertions;
using System.Collections.Generic;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Core.Models.Domain;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Models;

public class MissionPatchDataTests
{
    [Fact]
    public void ShouldInitializeWithDefaultValues()
    {
        // Act
        var subject = new MissionPatchData();

        // Assert
        subject.OrderedUnits.Should().BeNull();
        subject.Players.Should().BeNull();
        subject.Ranks.Should().BeNull();
        subject.Units.Should().BeNull();
    }

    [Fact]
    public void ShouldAllowSettingOrderedUnits()
    {
        // Arrange
        var subject = new MissionPatchData();
        var units = new List<MissionUnit> { new() { Callsign = "Alpha" }, new() { Callsign = "Bravo" } };

        // Act
        subject.OrderedUnits = units;

        // Assert
        subject.OrderedUnits.Should().BeSameAs(units);
        subject.OrderedUnits.Should().HaveCount(2);
        subject.OrderedUnits[0].Callsign.Should().Be("Alpha");
        subject.OrderedUnits[1].Callsign.Should().Be("Bravo");
    }

    [Fact]
    public void ShouldAllowSettingPlayers()
    {
        // Arrange
        var subject = new MissionPatchData();
        var players = new List<MissionPlayer> { new() { Name = "Player1" }, new() { Name = "Player2" } };

        // Act
        subject.Players = players;

        // Assert
        subject.Players.Should().BeSameAs(players);
        subject.Players.Should().HaveCount(2);
        subject.Players[0].Name.Should().Be("Player1");
        subject.Players[1].Name.Should().Be("Player2");
    }

    [Fact]
    public void ShouldAllowSettingRanks()
    {
        // Arrange
        var subject = new MissionPatchData();
        var ranks = new List<DomainRank> { new() { Name = "Private", Abbreviation = "Pte" }, new() { Name = "Sergeant", Abbreviation = "Sgt" } };

        // Act
        subject.Ranks = ranks;

        // Assert
        subject.Ranks.Should().BeSameAs(ranks);
        subject.Ranks.Should().HaveCount(2);
        subject.Ranks[0].Name.Should().Be("Private");
        subject.Ranks[1].Name.Should().Be("Sergeant");
    }

    [Fact]
    public void ShouldAllowSettingUnits()
    {
        // Arrange
        var subject = new MissionPatchData();
        var units = new List<MissionUnit> { new() { Callsign = "Charlie" }, new() { Callsign = "Delta" } };

        // Act
        subject.Units = units;

        // Assert
        subject.Units.Should().BeSameAs(units);
        subject.Units.Should().HaveCount(2);
        subject.Units[0].Callsign.Should().Be("Charlie");
        subject.Units[1].Callsign.Should().Be("Delta");
    }

    [Fact]
    public void Instance_ShouldAllowSettingStaticInstance()
    {
        // Arrange
        var patchData = new MissionPatchData { Players = new List<MissionPlayer> { new() { Name = "TestPlayer" } } };

        // Act
        MissionPatchData.Instance = patchData;

        // Assert
        MissionPatchData.Instance.Should().Be(patchData);
        MissionPatchData.Instance.Players.Should().HaveCount(1);
        MissionPatchData.Instance.Players[0].Name.Should().Be("TestPlayer");
    }

    [Fact]
    public void Instance_ShouldAllowNullAssignment()
    {
        // Arrange
        MissionPatchData.Instance = new MissionPatchData();

        // Act
        MissionPatchData.Instance = null;

        // Assert
        MissionPatchData.Instance.Should().BeNull();
    }

    [Fact]
    public void ShouldAllowNullCollectionAssignments()
    {
        // Arrange
        var subject = new MissionPatchData
        {
            OrderedUnits = new List<MissionUnit>(),
            Players = new List<MissionPlayer>(),
            Ranks = new List<DomainRank>(),
            Units = new List<MissionUnit>()
        };

        // Act
        subject.OrderedUnits = null;
        subject.Players = null;
        subject.Ranks = null;
        subject.Units = null;

        // Assert
        subject.OrderedUnits.Should().BeNull();
        subject.Players.Should().BeNull();
        subject.Ranks.Should().BeNull();
        subject.Units.Should().BeNull();
    }
}
