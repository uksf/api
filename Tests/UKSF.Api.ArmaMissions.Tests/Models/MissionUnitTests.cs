using FluentAssertions;
using System.Collections.Generic;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Core.Models.Domain;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Models;

public class MissionUnitTests
{
    [Fact]
    public void ShouldInitializeWithDefaultValues()
    {
        // Act
        var subject = new MissionUnit();

        // Assert
        subject.Callsign.Should().BeNull();
        subject.SourceUnit.Should().BeNull();
        subject.Members.Should().NotBeNull();
        subject.Members.Should().BeEmpty();
        subject.Roles.Should().NotBeNull();
        subject.Roles.Should().BeEmpty();
    }

    [Fact]
    public void ShouldAllowSettingCallsign()
    {
        // Arrange
        var subject = new MissionUnit();
        const string expectedCallsign = "Alpha-1";

        // Act
        subject.Callsign = expectedCallsign;

        // Assert
        subject.Callsign.Should().Be(expectedCallsign);
    }

    [Fact]
    public void ShouldAllowSettingSourceUnit()
    {
        // Arrange
        var subject = new MissionUnit();
        var sourceUnit = new DomainUnit { Id = "unit123", Name = "Test Unit" };

        // Act
        subject.SourceUnit = sourceUnit;

        // Assert
        subject.SourceUnit.Should().Be(sourceUnit);
        subject.SourceUnit.Id.Should().Be("unit123");
        subject.SourceUnit.Name.Should().Be("Test Unit");
    }

    [Fact]
    public void Members_ShouldUseModernCollectionInitialization()
    {
        // Arrange & Act
        var subject = new MissionUnit();

        // Assert
        subject.Members.Should().NotBeNull();
        subject.Members.Should().BeOfType<List<MissionPlayer>>();
    }

    [Fact]
    public void Roles_ShouldUseModernCollectionInitialization()
    {
        // Arrange & Act
        var subject = new MissionUnit();

        // Assert
        subject.Roles.Should().NotBeNull();
        subject.Roles.Should().BeOfType<Dictionary<string, MissionPlayer>>();
    }

    [Fact]
    public void ShouldAllowAddingMembers()
    {
        // Arrange
        var subject = new MissionUnit();
        var player = new MissionPlayer { Name = "TestPlayer" };

        // Act
        subject.Members.Add(player);

        // Assert
        subject.Members.Should().HaveCount(1);
        subject.Members[0].Should().Be(player);
        subject.Members[0].Name.Should().Be("TestPlayer");
    }

    [Fact]
    public void ShouldAllowAddingRoles()
    {
        // Arrange
        var subject = new MissionUnit();
        var player = new MissionPlayer { Name = "Leader" };
        const string roleKey = "Squad Leader";

        // Act
        subject.Roles.Add(roleKey, player);

        // Assert
        subject.Roles.Should().HaveCount(1);
        subject.Roles.Should().ContainKey(roleKey);
        subject.Roles[roleKey].Should().Be(player);
        subject.Roles[roleKey].Name.Should().Be("Leader");
    }

    [Fact]
    public void ShouldAllowMultipleMembers()
    {
        // Arrange
        var subject = new MissionUnit();
        var player1 = new MissionPlayer { Name = "Player1" };
        var player2 = new MissionPlayer { Name = "Player2" };

        // Act
        subject.Members.Add(player1);
        subject.Members.Add(player2);

        // Assert
        subject.Members.Should().HaveCount(2);
        subject.Members[0].Name.Should().Be("Player1");
        subject.Members[1].Name.Should().Be("Player2");
    }

    [Fact]
    public void ShouldAllowMultipleRoles()
    {
        // Arrange
        var subject = new MissionUnit();
        var leader = new MissionPlayer { Name = "Leader" };
        var medic = new MissionPlayer { Name = "Medic" };

        // Act
        subject.Roles.Add("Squad Leader", leader);
        subject.Roles.Add("Team Medic", medic);

        // Assert
        subject.Roles.Should().HaveCount(2);
        subject.Roles["Squad Leader"].Name.Should().Be("Leader");
        subject.Roles["Team Medic"].Name.Should().Be("Medic");
    }
}
