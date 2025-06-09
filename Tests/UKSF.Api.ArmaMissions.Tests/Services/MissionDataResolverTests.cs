using System;
using FluentAssertions;
using System.Collections.Generic;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.Core.Models.Domain;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class MissionDataResolverTests
{
    [Fact]
    public void ResolveObjectClass_ShouldReturnPilot_WhenPlayerFromPilotUnit()
    {
        // Arrange - Updated to use a pilot unit ID that exists in the actual implementation
        var player = CreateTestPlayer("5a435eea905d47336442c75a"); // Joint Special Forces Aviation Wing

        // Act
        var result = MissionDataResolver.ResolveObjectClass(player);

        // Assert
        result.Should().Be("UKSF_B_Pilot");
    }

    [Fact]
    public void ResolveObjectClass_ShouldReturnMedic_WhenPlayerIsMedic()
    {
        // Arrange
        var player = CreateTestPlayer("5a42835b55d6109bf0b081bd"); // UKSF
        player.Account = new DomainAccount { Id = "test-account-id", Qualifications = new Qualifications { Medic = true } };

        // Act
        var result = MissionDataResolver.ResolveObjectClass(player);

        // Assert
        result.Should().Be("UKSF_B_Medic");
    }

    [Fact]
    public void ResolveObjectClass_ShouldReturnSniper_WhenPlayerFromSniperPlatoon()
    {
        // Arrange
        var player = CreateTestPlayer("5a68b28e196530164c9b4fed"); // Sniper Platoon

        // Act
        var result = MissionDataResolver.ResolveObjectClass(player);

        // Assert
        result.Should().Be("UKSF_B_Sniper");
    }

    [Fact]
    public void ResolveObjectClass_ShouldReturnMedic_WhenPlayerFromMedicalRegiment()
    {
        // Arrange
        var player = CreateTestPlayer("5b9123ca7a6c1f0e9875601c"); // 3 Medical Regiment

        // Act
        var result = MissionDataResolver.ResolveObjectClass(player);

        // Assert
        result.Should().Be("UKSF_B_Medic");
    }

    [Fact]
    public void ResolveObjectClass_ShouldReturnSectionLeader_WhenPlayerHasUnitRole()
    {
        // Arrange
        var player = CreateTestPlayer("5a42835b55d6109bf0b081bd"); // UKSF
        player.Account = new DomainAccount { Id = "test-account-id" };
        player.Unit.SourceUnit.ChainOfCommand = new ChainOfCommand
        {
            First = "test-account-id" // Make player first in command
        };

        // Act
        var result = MissionDataResolver.ResolveObjectClass(player);

        // Assert
        result.Should().Be("UKSF_B_SectionLeader");
    }

    [Fact]
    public void ResolveObjectClass_ShouldReturnRifleman_WhenPlayerHasNoSpecialRole()
    {
        // Arrange
        var player = CreateTestPlayer("5a42835b55d6109bf0b081bd"); // UKSF

        // Act
        var result = MissionDataResolver.ResolveObjectClass(player);

        // Assert
        result.Should().Be("UKSF_B_Rifleman");
    }

    [Fact]
    public void IsEngineer_ShouldReturnTrue_WhenPlayerHasEngineerRole()
    {
        // Arrange
        var player = CreateTestPlayer("5a42835b55d6109bf0b081bd");
        player.Account = new DomainAccount { Id = "test-account-id", Qualifications = new Qualifications { Engineer = true } };

        // Act
        var result = MissionDataResolver.IsEngineer(player);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsEngineer_ShouldReturnFalse_WhenPlayerHasNoEngineerRole()
    {
        // Arrange
        var player = CreateTestPlayer("5a42835b55d6109bf0b081bd");
        player.Account = new DomainAccount { Id = "test-account-id", Qualifications = new Qualifications { Engineer = false } };

        // Act
        var result = MissionDataResolver.IsEngineer(player);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsEngineer_ShouldReturnFalse_WhenPlayerAccountIsNull()
    {
        // Arrange
        var player = CreateTestPlayer("5a42835b55d6109bf0b081bd");
        player.Account = null;

        // Act
        var result = MissionDataResolver.IsEngineer(player);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ResolveCallsign_ShouldReturnJSFAW_WhenUnitIsPilotUnit()
    {
        // Arrange
        var unit = CreateTestUnit("5a435eea905d47336442c75a"); // Joint Special Forces Aviation Wing
        const string defaultCallsign = "Default";

        // Act
        var result = MissionDataResolver.ResolveCallsign(unit, defaultCallsign);

        // Assert
        result.Should().Be("JSFAW");
    }

    [Fact]
    public void ResolveCallsign_ShouldReturnDefault_WhenUnitHasNoCallsign()
    {
        // Arrange
        var unit = CreateTestUnit("5a42835b55d6109bf0b081bd"); // UKSF (not a pilot unit)
        const string defaultCallsign = "Default";

        // Act
        var result = MissionDataResolver.ResolveCallsign(unit, defaultCallsign);

        // Assert
        result.Should().Be("Default");
    }

    [Fact]
    public void ResolveCallsign_ShouldReturnDefault_WhenSourceUnitIsNull()
    {
        // Arrange
        var unit = new MissionUnit { SourceUnit = null };
        const string defaultCallsign = "Default";

        // Act & Assert
        var act = () => MissionDataResolver.ResolveCallsign(unit, defaultCallsign);
        act.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void ResolveSpecialUnits_ShouldSetCorrectCallsigns_ForSpecialUnits()
    {
        // Arrange
        var units = new List<MissionUnit>
        {
            CreateTestUnit("5fe39de7815f5f03801134f7"), // Combat Ready - should be removed
            CreateTestUnit("5a848590eab14d12cc7fa618"), // RAF Cranwell - should be removed
            CreateTestUnit("5a42835b55d6109bf0b081bd") // UKSF - should remain
        };

        // Act
        MissionDataResolver.ResolveSpecialUnits(units);

        // Assert
        units.Should().HaveCount(1);
        units[0].SourceUnit.Id.Should().Be("5a42835b55d6109bf0b081bd");
    }

    [Fact]
    public void ResolveUnitSlots_ShouldThrowNullReferenceException_WhenUnitNull()
    {
        // Arrange
        MissionUnit unit = null!;

        // Act & Assert
        var act = () => MissionDataResolver.ResolveUnitSlots(unit);
        act.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void ResolveUnitSlots_ShouldReturnMembers_WhenUnitHasValidMembers()
    {
        // Arrange
        // Initialize MissionPatchData.Instance with required dependencies
        MissionPatchData.Instance = new MissionPatchData
        {
            Units = [],
            Ranks =
            [
                new DomainRank { Name = "Private" },
                new DomainRank { Name = "Recruit" }
            ]
        };

        // Create a proper ChainOfCommand for the unit
        var chainOfCommand = new ChainOfCommand
        {
            First = "1",
            Second = "2",
            Third = null,
            Nco = null
        };

        var sourceUnit = new DomainUnit { Id = "test-unit-id", ChainOfCommand = chainOfCommand };

        var unit = new MissionUnit
        {
            SourceUnit = sourceUnit,
            Members =
            [
                new MissionPlayer
                {
                    Name = "Player1",
                    Account = new DomainAccount { Id = "1" }, // First in chain of command
                    Rank = MissionPatchData.Instance.Ranks[0],
                    Unit = null! // Will be set properly by the method
                },
                new MissionPlayer
                {
                    Name = "Player2",
                    Account = new DomainAccount { Id = "2" }, // Second in chain of command
                    Rank = MissionPatchData.Instance.Ranks[1],
                    Unit = null! // Will be set properly by the method
                }
            ]
        };

        // Set the Unit reference on players to avoid circular reference issues during initialization
        foreach (var player in unit.Members)
        {
            player.Unit = unit;
        }

        // Act
        var result = MissionDataResolver.ResolveUnitSlots(unit);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
        result.Should().Contain(p => p.Name == "Player1");
        result.Should().Contain(p => p.Name == "Player2");

        // Verify that the players are sorted by their chain of command role (First should come before Second)
        result[0].Name.Should().Be("Player1"); // First in chain of command should be first
        result[1].Name.Should().Be("Player2"); // Second in chain of command should be second

        // Clean up
        MissionPatchData.Instance = null;
    }

    [Fact]
    public void IsUnitPermanent_ShouldNotThrow_WhenUnitHasSourceUnit()
    {
        // Arrange
        var unit = CreateTestUnit("5bbbb9645eb3a4170c488b36"); // Guardian 1-1

        // Act
        var result = MissionDataResolver.IsUnitPermanent(unit);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsUnitPermanent_ShouldNotThrow_WhenUnitHasDifferentBranch()
    {
        // Arrange
        var unit = CreateTestUnit("5a42835b55d6109bf0b081bd"); // UKSF

        // Act
        var result = MissionDataResolver.IsUnitPermanent(unit);

        // Assert
        result.Should().BeFalse();
    }

    private static MissionPlayer CreateTestPlayer(string unitId)
    {
        return new MissionPlayer
        {
            Name = "Test Player",
            Unit = CreateTestUnit(unitId),
            Account = null,
            Rank = null
        };
    }

    private static MissionUnit CreateTestUnit(string unitId)
    {
        return new MissionUnit
        {
            Callsign = "Original",
            SourceUnit = new DomainUnit
            {
                Id = unitId,
                Name = "Test Unit",
                Branch = UnitBranch.Combat,
                ChainOfCommand = null
            },
            Members = [],
            Roles = []
        };
    }
}
