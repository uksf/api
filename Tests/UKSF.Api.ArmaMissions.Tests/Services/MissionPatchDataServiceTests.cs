using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Services;

[Collection("MissionPatchData")]
public class MissionPatchDataServiceTests : IDisposable
{
    private readonly Mock<IRanksContext> _mockRanksContext = new();
    private readonly Mock<IAccountContext> _mockAccountContext = new();
    private readonly Mock<IUnitsContext> _mockUnitsContext = new();
    private readonly Mock<IRanksService> _mockRanksService = new();
    private readonly Mock<IDisplayNameService> _mockDisplayNameService = new();
    private readonly MissionPatchDataService _service;

    public MissionPatchDataServiceTests()
    {
        _service = new MissionPatchDataService(
            _mockRanksContext.Object,
            _mockAccountContext.Object,
            _mockUnitsContext.Object,
            _mockRanksService.Object,
            _mockDisplayNameService.Object
        );
    }

    public void Dispose()
    {
        MissionPatchData.Instance = null;
    }

    [Fact]
    public void UpdatePatchData_ShouldPopulateRanks()
    {
        var ranks = new List<DomainRank> { new() { Name = "Private", Order = 1 }, new() { Name = "Recruit", Order = 0 } };
        SetupMinimalData(ranks: ranks);

        _service.UpdatePatchData();

        MissionPatchData.Instance.Ranks.Should().HaveCount(2);
        MissionPatchData.Instance.Ranks.Should().Contain(r => r.Name == "Private");
        MissionPatchData.Instance.Ranks.Should().Contain(r => r.Name == "Recruit");
    }

    [Fact]
    public void UpdatePatchData_ShouldOnlyIncludeCombatUnits()
    {
        var parentId = ObjectId.GenerateNewId().ToString();
        var combatUnit = new DomainUnit
        {
            Id = parentId,
            Name = "Combat Unit",
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString()
        };
        var auxUnit = new DomainUnit
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Aux Unit",
            Branch = UnitBranch.Auxiliary
        };
        var allUnits = new List<DomainUnit> { combatUnit, auxUnit };

        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns((Func<DomainUnit, bool> predicate) => allUnits.Where(predicate));
        _mockRanksContext.Setup(x => x.Get()).Returns(new List<DomainRank>());
        _mockAccountContext.Setup(x => x.Get()).Returns(new List<DomainAccount>());

        _service.UpdatePatchData();

        MissionPatchData.Instance.Units.Should().HaveCount(1);
        MissionPatchData.Instance.Units[0].SourceUnit.Name.Should().Be("Combat Unit");
    }

    [Fact]
    public void UpdatePatchData_ShouldOnlyIncludePlayersWithRankAtLeastRecruit()
    {
        var parentId = ObjectId.GenerateNewId().ToString();
        var ranks = new List<DomainRank> { new() { Name = "Recruit", Order = 0 }, new() { Name = "Private", Order = 1 } };
        var qualifiedAccount = new DomainAccount
        {
            Id = "acc-1",
            Rank = "Private",
            UnitAssignment = "Root"
        };
        var unqualifiedAccount = new DomainAccount
        {
            Id = "acc-2",
            Rank = "Candidate",
            UnitAssignment = "Root"
        };
        var noRankAccount = new DomainAccount
        {
            Id = "acc-3",
            Rank = "",
            UnitAssignment = "Root"
        };

        var rootUnit = new DomainUnit
        {
            Id = parentId,
            Name = "Root",
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString(),
            Callsign = "Root",
            Members = ["acc-1"]
        };

        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new List<DomainUnit> { rootUnit });
        _mockRanksContext.Setup(x => x.Get()).Returns(ranks);
        _mockAccountContext.Setup(x => x.Get())
        .Returns(
            new List<DomainAccount>
            {
                qualifiedAccount,
                unqualifiedAccount,
                noRankAccount
            }
        );

        _mockRanksService.Setup(x => x.IsSuperiorOrEqual("Private", "Recruit")).Returns(true);
        _mockRanksService.Setup(x => x.IsSuperiorOrEqual("Candidate", "Recruit")).Returns(false);

        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(ranks[1]);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(qualifiedAccount)).Returns("Pte.Player1");

        _service.UpdatePatchData();

        MissionPatchData.Instance.Players.Should().HaveCount(1);
        MissionPatchData.Instance.Players[0].Name.Should().Be("Pte.Player1");
        MissionPatchData.Instance.Players[0].Account.Should().BeSameAs(qualifiedAccount);
        MissionPatchData.Instance.Players[0].Rank.Should().BeSameAs(ranks[1]);
    }

    [Fact]
    public void UpdatePatchData_ShouldResolveCallsigns()
    {
        var parentId = ObjectId.GenerateNewId().ToString();
        var unit = new DomainUnit
        {
            Id = parentId,
            Name = "Test Unit",
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString(),
            Callsign = "Alpha"
        };

        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new List<DomainUnit> { unit });
        _mockRanksContext.Setup(x => x.Get()).Returns(new List<DomainRank>());
        _mockAccountContext.Setup(x => x.Get()).Returns(new List<DomainAccount>());

        _service.UpdatePatchData();

        MissionPatchData.Instance.Units[0].Callsign.Should().Be("Alpha");
    }

    [Fact]
    public void UpdatePatchData_ShouldResolvePilotCallsign()
    {
        var pilotUnitId = "5a435eea905d47336442c75a"; // JSFAW
        var unit = new DomainUnit
        {
            Id = pilotUnitId,
            Name = "JSFAW",
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString(),
            Callsign = "Falcon"
        };

        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new List<DomainUnit> { unit });
        _mockRanksContext.Setup(x => x.Get()).Returns(new List<DomainRank>());
        _mockAccountContext.Setup(x => x.Get()).Returns(new List<DomainAccount>());

        _service.UpdatePatchData();

        MissionPatchData.Instance.Units[0].Callsign.Should().Be("JSFAW");
    }

    [Fact]
    public void UpdatePatchData_ShouldBuildChainOfCommandRoles()
    {
        var parentId = ObjectId.GenerateNewId().ToString();
        var unit = new DomainUnit
        {
            Id = parentId,
            Name = "Test Unit",
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString(),
            Callsign = "Alpha",
            Members = ["acc-1", "acc-2"],
            ChainOfCommand = new ChainOfCommand { First = "acc-1", Second = "acc-2" }
        };
        var ranks = new List<DomainRank> { new() { Name = "Private", Order = 1 } };
        var acc1 = new DomainAccount
        {
            Id = "acc-1",
            Rank = "Private",
            UnitAssignment = "Test Unit"
        };
        var acc2 = new DomainAccount
        {
            Id = "acc-2",
            Rank = "Private",
            UnitAssignment = "Test Unit"
        };

        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new List<DomainUnit> { unit });
        _mockRanksContext.Setup(x => x.Get()).Returns(ranks);
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(ranks[0]);
        _mockAccountContext.Setup(x => x.Get()).Returns(new List<DomainAccount> { acc1, acc2 });
        _mockRanksService.Setup(x => x.IsSuperiorOrEqual("Private", "Recruit")).Returns(true);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<DomainAccount>())).Returns<DomainAccount>(a => a.Id);

        _service.UpdatePatchData();

        var missionUnit = MissionPatchData.Instance.Units[0];
        missionUnit.Roles.Should().ContainKey("1iC");
        missionUnit.Roles["1iC"].Account.Id.Should().Be("acc-1");
        missionUnit.Roles.Should().ContainKey("2iC");
        missionUnit.Roles["2iC"].Account.Id.Should().Be("acc-2");
    }

    [Fact]
    public void UpdatePatchData_ShouldBuildChainOfCommand_WithThirdAndNco()
    {
        var parentId = ObjectId.GenerateNewId().ToString();
        var unit = new DomainUnit
        {
            Id = parentId,
            Name = "Test",
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString(),
            Callsign = "Alpha",
            Members = ["acc-3", "acc-4"],
            ChainOfCommand = new ChainOfCommand { Third = "acc-3", Nco = "acc-4" }
        };
        var ranks = new List<DomainRank> { new() { Name = "Private", Order = 1 } };
        var acc3 = new DomainAccount
        {
            Id = "acc-3",
            Rank = "Private",
            UnitAssignment = "Test"
        };
        var acc4 = new DomainAccount
        {
            Id = "acc-4",
            Rank = "Private",
            UnitAssignment = "Test"
        };

        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new List<DomainUnit> { unit });
        _mockRanksContext.Setup(x => x.Get()).Returns(ranks);
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(ranks[0]);
        _mockAccountContext.Setup(x => x.Get()).Returns(new List<DomainAccount> { acc3, acc4 });
        _mockRanksService.Setup(x => x.IsSuperiorOrEqual("Private", "Recruit")).Returns(true);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<DomainAccount>())).Returns<DomainAccount>(a => a.Id);

        _service.UpdatePatchData();

        var missionUnit = MissionPatchData.Instance.Units[0];
        missionUnit.Roles.Should().ContainKey("3iC");
        missionUnit.Roles["3iC"].Account.Id.Should().Be("acc-3");
        missionUnit.Roles.Should().ContainKey("NCOiC");
        missionUnit.Roles["NCOiC"].Account.Id.Should().Be("acc-4");
    }

    [Fact]
    public void UpdatePatchData_ShouldSkipRoles_WhenChainOfCommandIsNull()
    {
        var parentId = ObjectId.GenerateNewId().ToString();
        var unit = new DomainUnit
        {
            Id = parentId,
            Name = "Test",
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString(),
            Callsign = "Alpha",
            ChainOfCommand = null
        };

        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new List<DomainUnit> { unit });
        _mockRanksContext.Setup(x => x.Get()).Returns(new List<DomainRank>());
        _mockAccountContext.Setup(x => x.Get()).Returns(new List<DomainAccount>());

        _service.UpdatePatchData();

        MissionPatchData.Instance.Units[0].Roles.Should().BeEmpty();
    }

    [Fact]
    public void UpdatePatchData_ShouldResolvePlayerObjectClasses()
    {
        var parentId = ObjectId.GenerateNewId().ToString();
        var unit = new DomainUnit
        {
            Id = parentId,
            Name = "Test Unit",
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString(),
            Callsign = "Alpha",
            Members = ["acc-1"]
        };
        var ranks = new List<DomainRank> { new() { Name = "Private", Order = 1 } };
        var account = new DomainAccount
        {
            Id = "acc-1",
            Rank = "Private",
            UnitAssignment = "Test Unit"
        };

        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new List<DomainUnit> { unit });
        _mockRanksContext.Setup(x => x.Get()).Returns(ranks);
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(ranks[0]);
        _mockAccountContext.Setup(x => x.Get()).Returns(new List<DomainAccount> { account });
        _mockRanksService.Setup(x => x.IsSuperiorOrEqual("Private", "Recruit")).Returns(true);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<DomainAccount>())).Returns("Pte.Player");

        _service.UpdatePatchData();

        MissionPatchData.Instance.Players[0].ObjectClass.Should().Be("UKSF_B_Rifleman");
        MissionPatchData.Instance.Players[0].Unit.Should().NotBeNull();
    }

    [Fact]
    public void UpdatePatchData_ShouldOrderUnitsHierarchically()
    {
        var parentId = ObjectId.GenerateNewId().ToString();
        var childId = ObjectId.GenerateNewId().ToString();
        var grandchildId = ObjectId.GenerateNewId().ToString();

        var parent = new DomainUnit
        {
            Id = parentId,
            Name = "HQ",
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString(),
            Callsign = "HQ",
            Order = 0,
            Members = ["acc-1"]
        };
        var child = new DomainUnit
        {
            Id = childId,
            Name = "1 Squadron",
            Branch = UnitBranch.Combat,
            Parent = parentId,
            Callsign = "Alpha",
            Order = 0,
            Members = ["acc-2"]
        };
        var grandchild = new DomainUnit
        {
            Id = grandchildId,
            Name = "1 Section",
            Branch = UnitBranch.Combat,
            Parent = childId,
            Callsign = "Alpha-1",
            Order = 0,
            Members = ["acc-3"]
        };

        var ranks = new List<DomainRank> { new() { Name = "Private", Order = 1 } };
        var accounts = new List<DomainAccount>
        {
            new()
            {
                Id = "acc-1",
                Rank = "Private",
                UnitAssignment = "HQ"
            },
            new()
            {
                Id = "acc-2",
                Rank = "Private",
                UnitAssignment = "1 Squadron"
            },
            new()
            {
                Id = "acc-3",
                Rank = "Private",
                UnitAssignment = "1 Section"
            }
        };

        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>()))
        .Returns(
            new List<DomainUnit>
            {
                parent,
                child,
                grandchild
            }
        );
        _mockRanksContext.Setup(x => x.Get()).Returns(ranks);
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(ranks[0]);
        _mockAccountContext.Setup(x => x.Get()).Returns(accounts);
        _mockRanksService.Setup(x => x.IsSuperiorOrEqual("Private", "Recruit")).Returns(true);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<DomainAccount>())).Returns<DomainAccount>(a => a.Id);

        _service.UpdatePatchData();

        MissionPatchData.Instance.OrderedUnits.Should().HaveCount(3);
        MissionPatchData.Instance.OrderedUnits[0].SourceUnit.Name.Should().Be("HQ");
        MissionPatchData.Instance.OrderedUnits[1].SourceUnit.Name.Should().Be("1 Squadron");
        MissionPatchData.Instance.OrderedUnits[2].SourceUnit.Name.Should().Be("1 Section");
    }

    [Fact]
    public void UpdatePatchData_ShouldRemoveEmptyNonPermanentUnits()
    {
        var parentId = ObjectId.GenerateNewId().ToString();
        var emptyChildId = ObjectId.GenerateNewId().ToString();

        var parent = new DomainUnit
        {
            Id = parentId,
            Name = "HQ",
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString(),
            Callsign = "HQ",
            Members = ["acc-1"]
        };
        var emptyChild = new DomainUnit
        {
            Id = emptyChildId,
            Name = "Empty",
            Branch = UnitBranch.Combat,
            Parent = parentId,
            Callsign = "Empty"
        };

        var ranks = new List<DomainRank> { new() { Name = "Private", Order = 1 } };
        var account = new DomainAccount
        {
            Id = "acc-1",
            Rank = "Private",
            UnitAssignment = "HQ"
        };

        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new List<DomainUnit> { parent, emptyChild });
        _mockRanksContext.Setup(x => x.Get()).Returns(ranks);
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(ranks[0]);
        _mockAccountContext.Setup(x => x.Get()).Returns(new List<DomainAccount> { account });
        _mockRanksService.Setup(x => x.IsSuperiorOrEqual("Private", "Recruit")).Returns(true);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<DomainAccount>())).Returns("Pte.Player");

        _service.UpdatePatchData();

        MissionPatchData.Instance.OrderedUnits.Should().HaveCount(1);
        MissionPatchData.Instance.OrderedUnits[0].SourceUnit.Name.Should().Be("HQ");
    }

    [Fact]
    public void UpdatePatchData_ShouldKeepPermanentUnitsEvenWhenEmpty()
    {
        var parentId = ObjectId.GenerateNewId().ToString();
        var permanentId = "5bbbb9645eb3a4170c488b36"; // Guardian 1-1 (permanent)

        var parent = new DomainUnit
        {
            Id = parentId,
            Name = "HQ",
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString(),
            Callsign = "HQ",
            Members = ["acc-1"]
        };
        var permanent = new DomainUnit
        {
            Id = permanentId,
            Name = "Guardian 1-1",
            Branch = UnitBranch.Combat,
            Parent = parentId,
            Callsign = "Kestrel"
        };

        var ranks = new List<DomainRank> { new() { Name = "Private", Order = 1 } };
        var account = new DomainAccount
        {
            Id = "acc-1",
            Rank = "Private",
            UnitAssignment = "HQ"
        };

        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new List<DomainUnit> { parent, permanent });
        _mockRanksContext.Setup(x => x.Get()).Returns(ranks);
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(ranks[0]);
        _mockAccountContext.Setup(x => x.Get()).Returns(new List<DomainAccount> { account });
        _mockRanksService.Setup(x => x.IsSuperiorOrEqual("Private", "Recruit")).Returns(true);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<DomainAccount>())).Returns("Pte.Player");

        _service.UpdatePatchData();

        MissionPatchData.Instance.OrderedUnits.Should().Contain(u => u.SourceUnit.Id == permanentId);
    }

    [Fact]
    public void UpdatePatchData_ShouldRemoveUnitsWithoutCallsign()
    {
        var parentId = ObjectId.GenerateNewId().ToString();
        var noCallsignId = ObjectId.GenerateNewId().ToString();

        var parent = new DomainUnit
        {
            Id = parentId,
            Name = "HQ",
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString(),
            Callsign = "HQ",
            Members = ["acc-1"]
        };
        var noCallsign = new DomainUnit
        {
            Id = noCallsignId,
            Name = "NoCallsign",
            Branch = UnitBranch.Combat,
            Parent = parentId,
            Callsign = null,
            Members = ["acc-1"]
        };

        var ranks = new List<DomainRank> { new() { Name = "Private", Order = 1 } };
        var account = new DomainAccount
        {
            Id = "acc-1",
            Rank = "Private",
            UnitAssignment = "HQ"
        };

        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new List<DomainUnit> { parent, noCallsign });
        _mockRanksContext.Setup(x => x.Get()).Returns(ranks);
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(ranks[0]);
        _mockAccountContext.Setup(x => x.Get()).Returns(new List<DomainAccount> { account });
        _mockRanksService.Setup(x => x.IsSuperiorOrEqual("Private", "Recruit")).Returns(true);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<DomainAccount>())).Returns("Pte.Player");

        _service.UpdatePatchData();

        MissionPatchData.Instance.OrderedUnits.Should().NotContain(u => u.SourceUnit.Name == "NoCallsign");
    }

    [Fact]
    public void UpdatePatchData_ShouldRemoveSpecialUnits()
    {
        var parentId = ObjectId.GenerateNewId().ToString();
        var combatReadyId = "5fe39de7815f5f03801134f7";
        var rafCranwellId = "5a848590eab14d12cc7fa618";

        var parent = new DomainUnit
        {
            Id = parentId,
            Name = "HQ",
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString(),
            Callsign = "HQ",
            Members = ["acc-1"]
        };
        var combatReady = new DomainUnit
        {
            Id = combatReadyId,
            Name = "Combat Ready",
            Branch = UnitBranch.Combat,
            Parent = parentId,
            Callsign = "CombatReady",
            Members = ["acc-1"]
        };
        var rafCranwell = new DomainUnit
        {
            Id = rafCranwellId,
            Name = "RAF Cranwell",
            Branch = UnitBranch.Combat,
            Parent = parentId,
            Callsign = "Cranwell",
            Members = ["acc-1"]
        };

        var ranks = new List<DomainRank> { new() { Name = "Private", Order = 1 } };
        var account = new DomainAccount
        {
            Id = "acc-1",
            Rank = "Private",
            UnitAssignment = "HQ"
        };

        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>()))
        .Returns(
            new List<DomainUnit>
            {
                parent,
                combatReady,
                rafCranwell
            }
        );
        _mockRanksContext.Setup(x => x.Get()).Returns(ranks);
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(ranks[0]);
        _mockAccountContext.Setup(x => x.Get()).Returns(new List<DomainAccount> { account });
        _mockRanksService.Setup(x => x.IsSuperiorOrEqual("Private", "Recruit")).Returns(true);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<DomainAccount>())).Returns("Pte.Player");

        _service.UpdatePatchData();

        MissionPatchData.Instance.OrderedUnits.Should().NotContain(u => u.SourceUnit.Id == combatReadyId);
        MissionPatchData.Instance.OrderedUnits.Should().NotContain(u => u.SourceUnit.Id == rafCranwellId);
    }

    [Fact]
    public void UpdatePatchData_ShouldResolveMemberReferences()
    {
        var parentId = ObjectId.GenerateNewId().ToString();
        var unit = new DomainUnit
        {
            Id = parentId,
            Name = "Test",
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString(),
            Callsign = "Alpha",
            Members = ["acc-1", "acc-missing"]
        };
        var ranks = new List<DomainRank> { new() { Name = "Private", Order = 1 } };
        var account = new DomainAccount
        {
            Id = "acc-1",
            Rank = "Private",
            UnitAssignment = "Test"
        };

        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new List<DomainUnit> { unit });
        _mockRanksContext.Setup(x => x.Get()).Returns(ranks);
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(ranks[0]);
        _mockAccountContext.Setup(x => x.Get()).Returns(new List<DomainAccount> { account });
        _mockRanksService.Setup(x => x.IsSuperiorOrEqual("Private", "Recruit")).Returns(true);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<DomainAccount>())).Returns("Pte.Player");

        _service.UpdatePatchData();

        var missionUnit = MissionPatchData.Instance.Units[0];
        missionUnit.Members.Should().HaveCount(2);
        missionUnit.Members[0].Should().NotBeNull();
        missionUnit.Members[0].Account.Id.Should().Be("acc-1");
        missionUnit.Members[1].Should().BeNull();
    }

    [Fact]
    public void UpdatePatchData_ShouldOrderChildrenByOrder()
    {
        var parentId = ObjectId.GenerateNewId().ToString();
        var child1Id = ObjectId.GenerateNewId().ToString();
        var child2Id = ObjectId.GenerateNewId().ToString();

        var parent = new DomainUnit
        {
            Id = parentId,
            Name = "HQ",
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString(),
            Callsign = "HQ",
            Members = ["acc-1"]
        };
        var child1 = new DomainUnit
        {
            Id = child1Id,
            Name = "2 Squadron",
            Branch = UnitBranch.Combat,
            Parent = parentId,
            Callsign = "Bravo",
            Order = 1,
            Members = ["acc-2"]
        };
        var child2 = new DomainUnit
        {
            Id = child2Id,
            Name = "1 Squadron",
            Branch = UnitBranch.Combat,
            Parent = parentId,
            Callsign = "Alpha",
            Order = 0,
            Members = ["acc-3"]
        };

        var ranks = new List<DomainRank> { new() { Name = "Private", Order = 1 } };
        var accounts = new List<DomainAccount>
        {
            new()
            {
                Id = "acc-1",
                Rank = "Private",
                UnitAssignment = "HQ"
            },
            new()
            {
                Id = "acc-2",
                Rank = "Private",
                UnitAssignment = "2 Squadron"
            },
            new()
            {
                Id = "acc-3",
                Rank = "Private",
                UnitAssignment = "1 Squadron"
            }
        };

        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>()))
        .Returns(
            new List<DomainUnit>
            {
                parent,
                child1,
                child2
            }
        );
        _mockRanksContext.Setup(x => x.Get()).Returns(ranks);
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(ranks[0]);
        _mockAccountContext.Setup(x => x.Get()).Returns(accounts);
        _mockRanksService.Setup(x => x.IsSuperiorOrEqual("Private", "Recruit")).Returns(true);
        _mockDisplayNameService.Setup(x => x.GetDisplayName(It.IsAny<DomainAccount>())).Returns<DomainAccount>(a => a.Id);

        _service.UpdatePatchData();

        MissionPatchData.Instance.OrderedUnits[0].SourceUnit.Name.Should().Be("HQ");
        MissionPatchData.Instance.OrderedUnits[1].SourceUnit.Name.Should().Be("1 Squadron");
        MissionPatchData.Instance.OrderedUnits[2].SourceUnit.Name.Should().Be("2 Squadron");
    }

    private void SetupMinimalData(List<DomainRank> ranks = null, List<DomainAccount> accounts = null)
    {
        var parentId = ObjectId.GenerateNewId().ToString();
        var rootUnit = new DomainUnit
        {
            Id = parentId,
            Name = "Root",
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString(),
            Callsign = "Root"
        };

        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new List<DomainUnit> { rootUnit });
        _mockRanksContext.Setup(x => x.Get()).Returns(ranks ?? []);
        _mockAccountContext.Setup(x => x.Get()).Returns(accounts ?? []);
    }
}
