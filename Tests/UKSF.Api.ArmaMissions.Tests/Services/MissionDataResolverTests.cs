using System;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.Core.Models.Domain;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

[Collection("MissionPatchData")]
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
        const string DefaultCallsign = "Default";

        // Act
        var result = MissionDataResolver.ResolveCallsign(unit, DefaultCallsign);

        // Assert
        result.Should().Be("JSFAW");
    }

    [Fact]
    public void ResolveCallsign_ShouldReturnDefault_WhenUnitHasNoCallsign()
    {
        // Arrange
        var unit = CreateTestUnit("5a42835b55d6109bf0b081bd"); // UKSF (not a pilot unit)
        const string DefaultCallsign = "Default";

        // Act
        var result = MissionDataResolver.ResolveCallsign(unit, DefaultCallsign);

        // Assert
        result.Should().Be("Default");
    }

    [Fact]
    public void ResolveCallsign_ShouldReturnDefault_WhenSourceUnitIsNull()
    {
        // Arrange
        var unit = new MissionUnit { SourceUnit = null };
        const string DefaultCallsign = "Default";

        // Act & Assert
        var act = () => MissionDataResolver.ResolveCallsign(unit, DefaultCallsign);
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

    [Theory]
    [InlineData("5bbbb9645eb3a4170c488b36")] // Kestrel
    [InlineData("5bbbbdab5eb3a4170c488f2e")] // Raider
    [InlineData("5bbbbe365eb3a4170c488f30")] // Claymore
    [InlineData("5ad748e0de5d414f4c4055e0")] // Reserves
    public void IsUnitPermanent_ShouldReturnTrue_ForPermanentUnitIds(string unitId)
    {
        var unit = CreateTestUnit(unitId);
        MissionDataResolver.IsUnitPermanent(unit).Should().BeTrue();
    }

    [Fact]
    public void IsUnitPermanent_ShouldReturnFalse_ForNonPermanentUnit()
    {
        var unit = CreateTestUnit("5a42835b55d6109bf0b081bd"); // UKSF
        MissionDataResolver.IsUnitPermanent(unit).Should().BeFalse();
    }

    [Fact]
    public void ResolveUnitSlots_ShouldPlaceReserveSlotsAfterMembers_WhenChainOfCommandPositionsAreNull()
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

        // Create a ChainOfCommand where some positions are null (unassigned)
        var chainOfCommand = new ChainOfCommand
        {
            First = null, // Unassigned position
            Second = "real-member-id",
            Third = null, // Unassigned position
            Nco = null // Unassigned position
        };

        var sourceUnit = new DomainUnit { Id = "5bbbb9645eb3a4170c488b36", ChainOfCommand = chainOfCommand }; // Guardian 1-1

        var unit = new MissionUnit
        {
            SourceUnit = sourceUnit,
            Members =
            [
                // Real member with an account ID that matches a chain of command position
                new MissionPlayer
                {
                    Name = "RealMember",
                    Account = new DomainAccount { Id = "real-member-id" }, // Second in chain of command
                    Rank = MissionPatchData.Instance.Ranks[0],
                    Unit = null! // Will be set by ResolveUnitSlots
                }
            ]
        };

        // Set the Unit reference
        foreach (var player in unit.Members)
        {
            player.Unit = unit;
        }

        // Act - This will add Reserve slots and sort them
        var result = MissionDataResolver.ResolveUnitSlots(unit);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().BeGreaterThan(1); // Should have the real member plus Reserve slots

        // The real member should come first (higher priority role)
        result[0].Name.Should().Be("RealMember");
        result[0].Account.Should().NotBeNull();

        // Reserve slots should come after real members (they should have role -1, not match null positions)
        var reserveSlots = result.Where(p => p.Name == "Reserve").ToList();
        reserveSlots.Should().NotBeEmpty();

        // All Reserve slots should appear after the real member
        var realMemberIndex = result.FindIndex(p => p.Name == "RealMember");
        var firstReserveIndex = result.FindIndex(p => p.Name == "Reserve");

        firstReserveIndex.Should()
                         .BeGreaterThan(
                             realMemberIndex,
                             "Reserve slots should appear after real members, not before them due to null chain of command position matching"
                         );

        // Clean up
        MissionPatchData.Instance = null;
    }

    [Theory]
    [InlineData("5fe39de7815f5f03801134f7")] // Combat Ready
    [InlineData("5a848590eab14d12cc7fa618")] // RAF Cranwell
    public void ResolveObjectClass_ShouldReturnPilot_ForAllPilotUnitIds(string unitId)
    {
        var player = CreateTestPlayer(unitId);
        MissionDataResolver.ResolveObjectClass(player).Should().Be("UKSF_B_Pilot");
    }

    [Theory]
    [InlineData("5fe39de7815f5f03801134f7")] // Combat Ready
    [InlineData("5a848590eab14d12cc7fa618")] // RAF Cranwell
    public void ResolveCallsign_ShouldReturnJSFAW_ForAllPilotUnitIds(string unitId)
    {
        var unit = CreateTestUnit(unitId);
        MissionDataResolver.ResolveCallsign(unit, "Default").Should().Be("JSFAW");
    }

    [Fact]
    public void ResolveObjectClass_ShouldReturnSectionLeader_WhenPlayerIsSecondInCommand()
    {
        var player = CreateTestPlayer("5a42835b55d6109bf0b081bd");
        player.Account = new DomainAccount { Id = "test-id" };
        player.Unit.SourceUnit.ChainOfCommand = new ChainOfCommand { Second = "test-id" };

        MissionDataResolver.ResolveObjectClass(player).Should().Be("UKSF_B_SectionLeader");
    }

    [Fact]
    public void ResolveObjectClass_ShouldReturnSectionLeader_WhenPlayerIsThirdInCommand()
    {
        var player = CreateTestPlayer("5a42835b55d6109bf0b081bd");
        player.Account = new DomainAccount { Id = "test-id" };
        player.Unit.SourceUnit.ChainOfCommand = new ChainOfCommand { Third = "test-id" };

        MissionDataResolver.ResolveObjectClass(player).Should().Be("UKSF_B_SectionLeader");
    }

    [Fact]
    public void ResolveObjectClass_ShouldReturnSectionLeader_WhenPlayerIsNcoInCommand()
    {
        var player = CreateTestPlayer("5a42835b55d6109bf0b081bd");
        player.Account = new DomainAccount { Id = "test-id" };
        player.Unit.SourceUnit.ChainOfCommand = new ChainOfCommand { Nco = "test-id" };

        MissionDataResolver.ResolveObjectClass(player).Should().Be("UKSF_B_SectionLeader");
    }

    [Fact]
    public void ResolveObjectClass_ShouldReturnRifleman_WhenChainOfCommandIsNull()
    {
        var player = CreateTestPlayer("5a42835b55d6109bf0b081bd");
        player.Account = new DomainAccount { Id = "test-id" };
        player.Unit.SourceUnit.ChainOfCommand = null;

        MissionDataResolver.ResolveObjectClass(player).Should().Be("UKSF_B_Rifleman");
    }

    [Fact]
    public void ResolveObjectClass_ShouldReturnRifleman_WhenPlayerAccountIsNull()
    {
        var player = CreateTestPlayer("5a42835b55d6109bf0b081bd");
        player.Account = null;
        player.Unit.SourceUnit.ChainOfCommand = new ChainOfCommand { First = "someone-else" };

        MissionDataResolver.ResolveObjectClass(player).Should().Be("UKSF_B_Rifleman");
    }

    [Fact]
    public void ResolveObjectClass_ShouldReturnPilot_EvenWhenPlayerIsMedic_InPilotUnit()
    {
        var player = CreateTestPlayer("5a435eea905d47336442c75a");
        player.Account = new DomainAccount { Id = "test-id", Qualifications = new Qualifications { Medic = true } };

        // Pilot check comes before medic check
        MissionDataResolver.ResolveObjectClass(player).Should().Be("UKSF_B_Pilot");
    }

    [Fact]
    public void ResolveUnitSlots_ShouldAggregateMembers_ForJSFAW()
    {
        var ranks = new List<DomainRank> { new() { Name = "Private" } };
        MissionPatchData.Instance = new MissionPatchData { Units = [], Ranks = ranks };

        var jsfaw = CreateTestUnitWithMembers("5a435eea905d47336442c75a", ["PilotA"]);
        var combatReady = CreateTestUnitWithMembers("5fe39de7815f5f03801134f7", ["PilotB", "PilotC"]);
        var rafCranwell = CreateTestUnitWithMembers("5a848590eab14d12cc7fa618", ["PilotD"]);
        MissionPatchData.Instance.Units = [jsfaw, combatReady, rafCranwell];

        var result = MissionDataResolver.ResolveUnitSlots(jsfaw);

        result.Should().HaveCount(4);
        result.Select(p => p.Name).Should().BeEquivalentTo(["PilotA", "PilotB", "PilotC", "PilotD"]);

        MissionPatchData.Instance = null;
    }

    [Fact]
    public void ResolveUnitSlots_ShouldFillToMax3_ForSniperPlatoon()
    {
        MissionPatchData.Instance = new MissionPatchData { Units = [], Ranks = [new DomainRank { Name = "Private" }] };

        var unit = CreateTestUnitWithMembers("5a68b28e196530164c9b4fed", ["RealSniper"]);
        var result = MissionDataResolver.ResolveUnitSlots(unit);

        result.Should().HaveCount(3);
        result.Count(p => p.Name == "Sniper").Should().Be(2);
        result.Where(p => p.Name == "Sniper").Should().OnlyContain(p => p.ObjectClass == "UKSF_B_Sniper");
        result.Should().Contain(p => p.Name == "RealSniper");

        MissionPatchData.Instance = null;
    }

    [Theory]
    [InlineData("5bbbb9645eb3a4170c488b36")] // Kestrel
    [InlineData("5bbbbdab5eb3a4170c488f2e")] // Raider
    [InlineData("5bbbbe365eb3a4170c488f30")] // Claymore
    public void ResolveUnitSlots_ShouldFillToMax12_ForGuardianSquads(string unitId)
    {
        MissionPatchData.Instance = new MissionPatchData
        {
            Units = [],
            Ranks =
            [
                new DomainRank { Name = "Private" },
                new DomainRank { Name = "Recruit" }
            ]
        };

        var unit = CreateTestUnitWithMembers(unitId, ["Member1", "Member2"]);
        var result = MissionDataResolver.ResolveUnitSlots(unit);

        result.Should().HaveCount(12);
        result.Where(p => p.Name == "Reserve").Should().HaveCount(10);

        MissionPatchData.Instance = null;
    }

    [Fact]
    public void ResolveUnitSlots_ShouldSortByRank_WhenCoCPrioritiesEqual()
    {
        MissionPatchData.Instance = new MissionPatchData
        {
            Units = [],
            Ranks =
            [
                new DomainRank { Name = "Sergeant" },
                new DomainRank { Name = "Corporal" },
                new DomainRank { Name = "Private" }
            ]
        };

        var unit = new MissionUnit
        {
            SourceUnit = new DomainUnit { Id = "some-default-unit", ChainOfCommand = new ChainOfCommand() },
            Members =
            [
                new MissionPlayer
                {
                    Name = "PrivateJoe",
                    Account = new DomainAccount { Id = "p1" },
                    Rank = MissionPatchData.Instance.Ranks[2],
                    Unit = null!
                },
                new MissionPlayer
                {
                    Name = "SergeantSmith",
                    Account = new DomainAccount { Id = "p2" },
                    Rank = MissionPatchData.Instance.Ranks[0],
                    Unit = null!
                },
                new MissionPlayer
                {
                    Name = "CorporalDoe",
                    Account = new DomainAccount { Id = "p3" },
                    Rank = MissionPatchData.Instance.Ranks[1],
                    Unit = null!
                }
            ]
        };
        foreach (var p in unit.Members) p.Unit = unit;

        var result = MissionDataResolver.ResolveUnitSlots(unit);

        result[0].Name.Should().Be("SergeantSmith");
        result[1].Name.Should().Be("CorporalDoe");
        result[2].Name.Should().Be("PrivateJoe");

        MissionPatchData.Instance = null;
    }

    [Fact]
    public void ResolveUnitSlots_ShouldSortByName_WhenRankAndPriorityEqual()
    {
        MissionPatchData.Instance = new MissionPatchData { Units = [], Ranks = [new DomainRank { Name = "Private" }] };

        var unit = new MissionUnit
        {
            SourceUnit = new DomainUnit { Id = "some-default-unit", ChainOfCommand = new ChainOfCommand() },
            Members =
            [
                new MissionPlayer
                {
                    Name = "Zulu",
                    Account = new DomainAccount { Id = "z" },
                    Rank = MissionPatchData.Instance.Ranks[0],
                    Unit = null!
                },
                new MissionPlayer
                {
                    Name = "Alpha",
                    Account = new DomainAccount { Id = "a" },
                    Rank = MissionPatchData.Instance.Ranks[0],
                    Unit = null!
                }
            ]
        };
        foreach (var p in unit.Members) p.Unit = unit;

        var result = MissionDataResolver.ResolveUnitSlots(unit);

        result[0].Name.Should().Be("Alpha");
        result[1].Name.Should().Be("Zulu");

        MissionPatchData.Instance = null;
    }

    [Fact]
    public void ResolveUnitSlots_ShouldReturnMembersDirectly_ForDefaultUnit()
    {
        MissionPatchData.Instance = new MissionPatchData { Units = [], Ranks = [new DomainRank { Name = "Private" }] };

        var unit = new MissionUnit
        {
            SourceUnit = new DomainUnit { Id = "some-random-unit", ChainOfCommand = new ChainOfCommand() },
            Members =
            [
                new MissionPlayer
                {
                    Name = "Player1",
                    Account = new DomainAccount { Id = "p1" },
                    Rank = MissionPatchData.Instance.Ranks[0],
                    Unit = null!
                }
            ]
        };
        foreach (var p in unit.Members) p.Unit = unit;

        var result = MissionDataResolver.ResolveUnitSlots(unit);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Player1");

        MissionPatchData.Instance = null;
    }

    [Fact]
    public void ResolveUnitSlots_ShouldIncludeMembersAndFillReserves_ForGuardian1R()
    {
        // Arrange
        MissionPatchData.Instance = new MissionPatchData
        {
            Units = [],
            Ranks =
            [
                new DomainRank { Name = "Private" },
                new DomainRank { Name = "Recruit" }
            ]
        };

        var chainOfCommand = new ChainOfCommand
        {
            First = "member-1",
            Second = null,
            Third = null,
            Nco = null
        };

        var sourceUnit = new DomainUnit { Id = "5ad748e0de5d414f4c4055e0", ChainOfCommand = chainOfCommand }; // Guardian 1-R

        var unit = new MissionUnit
        {
            SourceUnit = sourceUnit,
            Members =
            [
                new MissionPlayer
                {
                    Name = "Member1",
                    Account = new DomainAccount { Id = "member-1" },
                    Rank = MissionPatchData.Instance.Ranks[0],
                    Unit = null!
                },
                new MissionPlayer
                {
                    Name = "Member2",
                    Account = new DomainAccount { Id = "member-2" },
                    Rank = MissionPatchData.Instance.Ranks[0],
                    Unit = null!
                }
            ]
        };

        foreach (var player in unit.Members)
        {
            player.Unit = unit;
        }

        // Act
        var result = MissionDataResolver.ResolveUnitSlots(unit);

        // Assert
        result.Should().HaveCount(10); // max is 10 for Guardian 1-R
        result.Where(p => p.Name == "Reserve").Should().HaveCount(8); // 10 - 2 members = 8 reserves
        result[0].Name.Should().Be("Member1"); // Real members sorted first
        result[1].Name.Should().Be("Member2");

        // Clean up
        MissionPatchData.Instance = null;
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

    private static MissionUnit CreateTestUnitWithMembers(string unitId, string[] memberNames)
    {
        var unit = CreateTestUnit(unitId);
        unit.Members = memberNames.Select(name => new MissionPlayer
                                      {
                                          Name = name,
                                          Account = new DomainAccount { Id = $"id-{name}" },
                                          Rank = MissionPatchData.Instance.Ranks[0],
                                          Unit = unit
                                      }
                                  )
                                  .ToList();
        return unit;
    }
}
