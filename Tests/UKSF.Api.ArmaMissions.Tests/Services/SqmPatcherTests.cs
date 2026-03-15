using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Models.Sqm;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.Core.Models.Domain;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class SqmPatcherTests
{
    private readonly SqmPatcher _patcher = new();

    // ─── Medic Trait ────────────────────────────────────────────────────────

    [Fact]
    public void Patch_MedicPlayer_GetsMedicTraitAttribute()
    {
        var context = CreateContextWithSinglePlayer(isMedic: true, isEngineer: false);

        _patcher.Patch(context);

        var playerLines = GetFirstPlayerEntityLines(context);
        playerLines.Should().Contain(l => l.Contains("Enh_unitTraits_medic"));
        playerLines.Should().Contain(l => l.Contains("setUnitTrait ['Medic',_value]"));
    }

    [Fact]
    public void Patch_NonMedicPlayer_NoMedicTraitAttribute()
    {
        var context = CreateContextWithSinglePlayer(isMedic: false, isEngineer: false);

        _patcher.Patch(context);

        var playerLines = GetFirstPlayerEntityLines(context);
        playerLines.Should().NotContain(l => l.Contains("Enh_unitTraits_medic"));
    }

    // ─── Engineer Trait ─────────────────────────────────────────────────────

    [Fact]
    public void Patch_EngineerPlayer_GetsEngineerTraitAttribute()
    {
        var context = CreateContextWithSinglePlayer(isMedic: false, isEngineer: true);

        _patcher.Patch(context);

        var playerLines = GetFirstPlayerEntityLines(context);
        playerLines.Should().Contain(l => l.Contains("Enh_unitTraits_engineer"));
        playerLines.Should().Contain(l => l.Contains("setUnitTrait ['Engineer',_value]"));
    }

    [Fact]
    public void Patch_NonEngineerPlayer_NoEngineerTraitAttribute()
    {
        var context = CreateContextWithSinglePlayer(isMedic: false, isEngineer: false);

        _patcher.Patch(context);

        var playerLines = GetFirstPlayerEntityLines(context);
        playerLines.Should().NotContain(l => l.Contains("Enh_unitTraits_engineer"));
    }

    // ─── Both Traits ────────────────────────────────────────────────────────

    [Fact]
    public void Patch_MedicAndEngineerPlayer_GetsBothTraitAttributes()
    {
        var context = CreateContextWithSinglePlayer(isMedic: true, isEngineer: true);

        _patcher.Patch(context);

        var playerLines = GetFirstPlayerEntityLines(context);
        playerLines.Should().Contain(l => l.Contains("Enh_unitTraits_medic"));
        playerLines.Should().Contain(l => l.Contains("Enh_unitTraits_engineer"));
    }

    [Fact]
    public void Patch_MedicAndEngineerPlayer_HasCorrectAttributeCount()
    {
        var context = CreateContextWithSinglePlayer(isMedic: true, isEngineer: true);

        _patcher.Patch(context);

        var playerLines = GetFirstPlayerEntityLines(context);
        playerLines.Should().Contain(l => l.Contains("nAttributes=2;"));
    }

    [Fact]
    public void Patch_SingleTraitPlayer_HasCorrectAttributeCount()
    {
        var context = CreateContextWithSinglePlayer(isMedic: true, isEngineer: false);

        _patcher.Patch(context);

        var playerLines = GetFirstPlayerEntityLines(context);
        playerLines.Should().Contain(l => l.Contains("nAttributes=1;"));
    }

    [Fact]
    public void Patch_NoTraitsPlayer_NoCustomAttributesBlock()
    {
        var context = CreateContextWithSinglePlayer(isMedic: false, isEngineer: false);

        _patcher.Patch(context);

        var playerLines = GetFirstPlayerEntityLines(context);
        playerLines.Should().NotContain(l => l.Contains("CustomAttributes"));
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static MissionPatchContext CreateContextWithSinglePlayer(bool isMedic, bool isEngineer)
    {
        var player = new PatchPlayer
        {
            DisplayName = "Test Player",
            ObjectClass = "UKSF_B_Rifleman",
            Callsign = "TestCallsign",
            IsMedic = isMedic,
            IsEngineer = isEngineer,
            Rank = new DomainRank
            {
                Name = "Private",
                Abbreviation = "Pte",
                Order = 0
            }
        };

        var unit = new PatchUnit
        {
            Source = new DomainUnit
            {
                Id = "testunit",
                Name = "Test Unit",
                Parent = "00000000000000000000000",
                Callsign = "TestCallsign",
                Branch = UnitBranch.Combat,
                ChainOfCommand = new ChainOfCommand()
            },
            Callsign = "TestCallsign",
            Slots = [player]
        };

        return new MissionPatchContext
        {
            PatchData = new PatchData { Ranks = [player.Rank], OrderedUnits = [unit] },
            Sqm = new SqmDocument { Entities = [] },
            MaxCurators = 0
        };
    }

    private static List<string> GetFirstPlayerEntityLines(MissionPatchContext context)
    {
        var group = context.Sqm.Entities.OfType<SqmGroup>().First();
        var playerEntity = group.Children.OfType<SqmObject>().First();
        return playerEntity.RawLines;
    }
}
