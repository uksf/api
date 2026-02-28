using System.Linq;
using FluentAssertions;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.ArmaMissions.Tests.Helpers;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

public class PatchDataBuilderTests
{
    private readonly TestPatchDataBuilder _testData = new();
    private readonly PatchDataBuilder _builder;
    private readonly MissionPatchContext _context = new();

    public PatchDataBuilderTests()
    {
        _builder = _testData.BuildPatchDataBuilder();
        _builder.Build(_context);
    }

    // ─── Ranks ────────────────────────────────────────────────────────────

    [Fact]
    public void Build_PopulatesAllRanks()
    {
        _context.PatchData.Ranks.Should().HaveCount(6);
        _context.PatchData.Ranks.Should()
                .ContainInOrder(
                    _testData.RankRecruit,
                    _testData.RankPrivate,
                    _testData.RankCorporal,
                    _testData.RankSergeant,
                    _testData.RankWo2,
                    _testData.RankMajor
                );
    }

    // ─── Unit Ordering and Pruning ────────────────────────────────────────

    [Fact]
    public void Build_ProducesCorrectNumberOfOrderedUnits()
    {
        // UKSF root: no members, not permanent → pruned
        // CombatReady: aggregated into JSFAW → removed
        // RAF Cranwell: no members, not permanent → pruned
        // Empty Unit: no members, not permanent, no callsign → pruned
        // Remaining: SFSG, Guardian 1-1, 1-2, 1-3, 1-R, JSFAW, Sniper Platoon, 3 Medical Regiment
        _context.PatchData.OrderedUnits.Should().HaveCount(8);
    }

    [Fact]
    public void Build_ProducesCorrectUnitOrder()
    {
        var names = _context.PatchData.OrderedUnits.Select(u => u.Source.Name).ToList();
        names.Should()
             .ContainInOrder(
                 "SFSG",
                 "Guardian 1-1",
                 "Guardian 1-2",
                 "Guardian 1-3",
                 "Guardian 1-R",
                 "Joint Special Forces Aviation Wing",
                 "Sniper Platoon",
                 "3 Medical Regiment"
             );
    }

    [Fact]
    public void Build_ExcludesUksfRootUnit()
    {
        _context.PatchData.OrderedUnits.Should().NotContain(u => u.Source.Name == "UKSF");
    }

    [Fact]
    public void Build_ExcludesCombatReadyUnit()
    {
        _context.PatchData.OrderedUnits.Should().NotContain(u => u.Source.Name == "Combat Ready");
    }

    [Fact]
    public void Build_ExcludesRafCranwellUnit()
    {
        _context.PatchData.OrderedUnits.Should().NotContain(u => u.Source.Name == "RAF Cranwell");
    }

    [Fact]
    public void Build_ExcludesEmptyUnit()
    {
        _context.PatchData.OrderedUnits.Should().NotContain(u => u.Source.Name == "Empty Unit");
    }

    // ─── Callsigns ────────────────────────────────────────────────────────

    [Fact]
    public void Build_SfsgHasCorrectCallsign()
    {
        var sfsg = GetUnit("SFSG");
        sfsg.Callsign.Should().Be("Guardian");
    }

    [Fact]
    public void Build_PilotUnitsGetJsfawCallsign()
    {
        var jsfaw = GetUnit("Joint Special Forces Aviation Wing");
        jsfaw.Callsign.Should().Be("JSFAW");
    }

    [Fact]
    public void Build_GuardianUnitsHaveCorrectCallsigns()
    {
        GetUnit("Guardian 1-1").Callsign.Should().Be("Kestrel");
        GetUnit("Guardian 1-2").Callsign.Should().Be("Raider");
        GetUnit("Guardian 1-3").Callsign.Should().Be("Claymore");
        GetUnit("Guardian 1-R").Callsign.Should().Be("Reserves");
    }

    // ─── Permanent Units and Fillers ──────────────────────────────────────

    [Fact]
    public void Build_PermanentUnitWithMembersFilledToMaxSlots()
    {
        // Guardian 1-1: MaxSlots=12, has SgtAlpha, CplBravo, PteCharlie (3 real members)
        var g11 = GetUnit("Guardian 1-1");
        g11.Slots.Should().HaveCount(12);
    }

    [Fact]
    public void Build_PermanentUnitWithNoMembersFilledToMaxSlots()
    {
        // Guardian 1-2: MaxSlots=12, no members
        var g12 = GetUnit("Guardian 1-2");
        g12.Slots.Should().HaveCount(12);
    }

    [Fact]
    public void Build_PermanentUnitFillersHaveCorrectDisplayName()
    {
        var g11 = GetUnit("Guardian 1-1");
        var fillers = g11.Slots.Where(p => p.DisplayName == "Reserve").ToList();
        fillers.Should().HaveCount(9);
    }

    [Fact]
    public void Build_PermanentUnitFillersHaveRecruitRank()
    {
        var g12 = GetUnit("Guardian 1-2");
        g12.Slots.Should().OnlyContain(p => p.Rank == _testData.RankRecruit);
    }

    [Fact]
    public void Build_SniperPlatoonFilledToMaxSlots()
    {
        // SniperPlatoon: MaxSlots=3, has SniperGolf (1 real member)
        var snipers = GetUnit("Sniper Platoon");
        snipers.Slots.Should().HaveCount(3);
    }

    [Fact]
    public void Build_SniperPlatoonFillersHaveCorrectNameAndRank()
    {
        var snipers = GetUnit("Sniper Platoon");
        var fillers = snipers.Slots.Where(p => p.DisplayName == "Sniper").ToList();
        fillers.Should().HaveCount(2);
        fillers.Should().OnlyContain(p => p.Rank == _testData.RankPrivate);
    }

    // ─── Aggregation ──────────────────────────────────────────────────────

    [Fact]
    public void Build_CombatReadyMembersAggregatedIntoJsfaw()
    {
        // PilotFoxtrot is in CombatReady which aggregates into JSFAW
        var jsfaw = GetUnit("Joint Special Forces Aviation Wing");
        jsfaw.Slots.Should().Contain(p => p.DisplayName == "Pte. Foxtrot.M");
    }

    [Fact]
    public void Build_JsfawContainsAllPilots()
    {
        var jsfaw = GetUnit("Joint Special Forces Aviation Wing");
        jsfaw.Slots.Should().Contain(p => p.DisplayName == "Cpl. Delta.S");
        jsfaw.Slots.Should().Contain(p => p.DisplayName == "Pte. Echo.D");
        jsfaw.Slots.Should().Contain(p => p.DisplayName == "Pte. Foxtrot.M");
    }

    // ─── Object Classes ───────────────────────────────────────────────────

    [Fact]
    public void Build_PilotUnitMembersGetPilotObjectClass()
    {
        var jsfaw = GetUnit("Joint Special Forces Aviation Wing");
        jsfaw.Slots.Should().OnlyContain(p => p.ObjectClass == "UKSF_B_Pilot");
    }

    [Fact]
    public void Build_SniperPlatoonMembersGetForcedSniperObjectClass()
    {
        var snipers = GetUnit("Sniper Platoon");
        snipers.Slots.Should().OnlyContain(p => p.ObjectClass == "UKSF_B_Sniper");
    }

    [Fact]
    public void Build_MedicalRegimentMembersGetForcedMedicObjectClass()
    {
        var medics = GetUnit("3 Medical Regiment");
        medics.Slots.Should().OnlyContain(p => p.ObjectClass == "UKSF_B_Medic");
    }

    [Fact]
    public void Build_MedicQualifiedMemberGetsMedicObjectClass()
    {
        // CplBravo has Medic qualification, is in Guardian 1-1 (no forced class, not pilot)
        var g11 = GetUnit("Guardian 1-1");
        var cplBravo = g11.Slots.Single(p => p.DisplayName == "Cpl. Bravo.J");
        cplBravo.ObjectClass.Should().Be("UKSF_B_Medic");
    }

    [Fact]
    public void Build_ChainOfCommandMembersGetSectionLeaderObjectClass()
    {
        // MajSmith is First in SFSG CoC
        var sfsg = GetUnit("SFSG");
        var majSmith = sfsg.Slots.Single(p => p.DisplayName == "Maj. Smith.J");
        majSmith.ObjectClass.Should().Be("UKSF_B_SectionLeader");

        // Wo2Jones is NCO in SFSG CoC
        var wo2Jones = sfsg.Slots.Single(p => p.DisplayName == "WO2. Jones.D");
        wo2Jones.ObjectClass.Should().Be("UKSF_B_SectionLeader");

        // SgtAlpha is First in Guardian 1-1 CoC
        var g11 = GetUnit("Guardian 1-1");
        var sgtAlpha = g11.Slots.Single(p => p.DisplayName == "Sgt. Alpha.T");
        sgtAlpha.ObjectClass.Should().Be("UKSF_B_SectionLeader");
    }

    [Fact]
    public void Build_StandardMembersGetRiflemanObjectClass()
    {
        // PteCharlie: no qualifications, not in CoC, not a pilot
        var g11 = GetUnit("Guardian 1-1");
        var pteCharlie = g11.Slots.Single(p => p.DisplayName == "Pte. Charlie.B");
        pteCharlie.ObjectClass.Should().Be("UKSF_B_Rifleman");
    }

    // ─── Engineer Flag ────────────────────────────────────────────────────

    [Fact]
    public void Build_EngineerQualifiedMembersHaveIsEngineerTrue()
    {
        var sfsg = GetUnit("SFSG");
        sfsg.Slots.Single(p => p.DisplayName == "Maj. Smith.J").IsEngineer.Should().BeTrue();
        sfsg.Slots.Single(p => p.DisplayName == "WO2. Jones.D").IsEngineer.Should().BeTrue();
    }

    [Fact]
    public void Build_NonEngineerMembersHaveIsEngineerFalse()
    {
        var g11 = GetUnit("Guardian 1-1");
        g11.Slots.Single(p => p.DisplayName == "Sgt. Alpha.T").IsEngineer.Should().BeFalse();
        g11.Slots.Single(p => p.DisplayName == "Cpl. Bravo.J").IsEngineer.Should().BeFalse();
        g11.Slots.Single(p => p.DisplayName == "Pte. Charlie.B").IsEngineer.Should().BeFalse();
    }

    // ─── Slot Sort Order ──────────────────────────────────────────────────

    [Fact]
    public void Build_SlotsOrderedByCocPriorityThenRankThenName()
    {
        // SFSG: MajSmith (First=priority 3), Wo2Jones (Nco=priority 0) — both CoC
        // MajSmith outranks Wo2Jones by CoC priority
        var sfsg = GetUnit("SFSG");
        var names = sfsg.Slots.Select(p => p.DisplayName).ToList();
        names[0].Should().Be("Maj. Smith.J");
        names[1].Should().Be("WO2. Jones.D");
    }

    [Fact]
    public void Build_SlotsOrderedByRankAscendingWhenNoCocPriority()
    {
        // Sort is rank index ascending: lower rank index = lower rank = sorted first.
        // Guardian 1-1: SgtAlpha (CoC First=priority 3) comes first,
        // then fillers (Recruit=order 0) before PteCharlie (Pte=order 1) before CplBravo (Cpl=order 2).
        var g11 = GetUnit("Guardian 1-1");
        var names = g11.Slots.Select(p => p.DisplayName).ToList();
        var sgtIndex = names.IndexOf("Sgt. Alpha.T");
        var pteIndex = names.IndexOf("Pte. Charlie.B");
        var cplIndex = names.IndexOf("Cpl. Bravo.J");

        sgtIndex.Should().Be(0, "SgtAlpha has CoC priority and sorts first");
        pteIndex.Should().BeLessThan(cplIndex, "Private rank (order 1) sorts before Corporal rank (order 2)");
    }

    // ─── Callsign on PatchPlayer ──────────────────────────────────────────

    [Fact]
    public void Build_AllSlotsInUnitHaveUnitCallsign()
    {
        var jsfaw = GetUnit("Joint Special Forces Aviation Wing");
        jsfaw.Slots.Should().OnlyContain(p => p.Callsign == "JSFAW");
    }

    [Fact]
    public void Build_GuardianSlotsHaveGuardianSquadCallsign()
    {
        var g11 = GetUnit("Guardian 1-1");
        g11.Slots.Should().OnlyContain(p => p.Callsign == "Kestrel");
    }

    // ─── Rank on PatchPlayer ──────────────────────────────────────────────

    [Fact]
    public void Build_PlayerRankMatchesDomainRank()
    {
        var sfsg = GetUnit("SFSG");
        sfsg.Slots.Single(p => p.DisplayName == "Maj. Smith.J").Rank.Should().Be(_testData.RankMajor);
        sfsg.Slots.Single(p => p.DisplayName == "WO2. Jones.D").Rank.Should().Be(_testData.RankWo2);
    }

    // ─── Display Names ────────────────────────────────────────────────────

    [Fact]
    public void Build_DisplayNamesFormattedCorrectly()
    {
        var sfsg = GetUnit("SFSG");
        sfsg.Slots.Should().Contain(p => p.DisplayName == "Maj. Smith.J");
        sfsg.Slots.Should().Contain(p => p.DisplayName == "WO2. Jones.D");

        var g11 = GetUnit("Guardian 1-1");
        g11.Slots.Should().Contain(p => p.DisplayName == "Sgt. Alpha.T");
        g11.Slots.Should().Contain(p => p.DisplayName == "Cpl. Bravo.J");
        g11.Slots.Should().Contain(p => p.DisplayName == "Pte. Charlie.B");
    }

    // ─── PatchData Source Unit Reference ─────────────────────────────────

    [Fact]
    public void Build_PatchUnitSourceReferencesOriginalDomainUnit()
    {
        GetUnit("SFSG").Source.Should().BeSameAs(_testData.UnitSfsg);
        GetUnit("Guardian 1-1").Source.Should().BeSameAs(_testData.UnitGuardian11);
        GetUnit("Joint Special Forces Aviation Wing").Source.Should().BeSameAs(_testData.UnitJsfaw);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private PatchUnit GetUnit(string name)
    {
        return _context.PatchData.OrderedUnits.Single(u => u.Source.Name == name);
    }
}
