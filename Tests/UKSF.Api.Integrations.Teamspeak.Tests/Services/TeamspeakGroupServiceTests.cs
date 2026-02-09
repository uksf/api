using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Mappers;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Integrations.Teamspeak.Models;
using UKSF.Api.Integrations.Teamspeak.Services;
using Xunit;

namespace UKSF.Api.Integrations.Teamspeak.Tests.Services;

public class TeamspeakGroupServiceTests
{
    private static readonly DomainVariableItem TeamspeakGidUnverified = new() { Key = "TEAMSPEAK_GID_UNVERIFIED", Item = "1" };
    private static readonly DomainVariableItem TeamspeakGidDischarged = new() { Key = "TEAMSPEAK_GID_DISCHARGED", Item = "2" };
    private static readonly DomainVariableItem TeamspeakGidRoot = new() { Key = "TEAMSPEAK_GID_ROOT", Item = "3" };
    private static readonly DomainVariableItem TeamspeakGidElcom = new() { Key = "TEAMSPEAK_GID_ELCOM", Item = "4" };
    private static readonly DomainVariableItem TeamspeakGidBlacklist = new() { Key = "TEAMSPEAK_GID_BLACKLIST", Item = "99,100" };

    private readonly List<int> _addedGroups = [];

    private readonly DomainUnit _elcomUnit = new()
    {
        Id = ObjectId.GenerateNewId().ToString(),
        Name = "ELCOM",
        Branch = UnitBranch.Auxiliary,
        Parent = ObjectId.Empty.ToString()
    };

    private readonly Mock<IRanksContext> _mockRanksContext = new();
    private readonly Mock<ITeamspeakManagerService> _mockTeamspeakManagerService = new();
    private readonly Mock<ITrainingsContext> _mockTrainingsContext = new();
    private readonly Mock<IUnitsContext> _mockUnitsContext = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly List<int> _removedGroups = [];
    private readonly TeamspeakGroupService _teamspeakGroupService;

    public TeamspeakGroupServiceTests()
    {
        _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_UNVERIFIED")).Returns(TeamspeakGidUnverified);
        _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_DISCHARGED")).Returns(TeamspeakGidDischarged);
        _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_ROOT")).Returns(TeamspeakGidRoot);
        _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_ELCOM")).Returns(TeamspeakGidElcom);
        _mockVariablesService.Setup(x => x.GetVariable("TEAMSPEAK_GID_BLACKLIST")).Returns(TeamspeakGidBlacklist);

        _mockTeamspeakManagerService.Setup(x => x.SendGroupProcedure(TeamspeakProcedureType.Assign, It.IsAny<TeamspeakGroupProcedure>()))
                                    .Returns(Task.CompletedTask)
                                    .Callback((TeamspeakProcedureType _, TeamspeakGroupProcedure groupProcedure) => _addedGroups.Add(groupProcedure.ServerGroup)
                                    );
        _mockTeamspeakManagerService.Setup(x => x.SendGroupProcedure(TeamspeakProcedureType.Unassign, It.IsAny<TeamspeakGroupProcedure>()))
                                    .Returns(Task.CompletedTask)
                                    .Callback((TeamspeakProcedureType _, TeamspeakGroupProcedure groupProcedure) =>
                                                  _removedGroups.Add(groupProcedure.ServerGroup)
                                    );

        var unitsService = new UnitsService(
            _mockUnitsContext.Object,
            new Mock<IRanksService>().Object,
            new Mock<IChainOfCommandService>().Object,
            new Mock<IDisplayNameService>().Object,
            new Mock<IAccountContext>().Object,
            new UnitMapper()
        );
        _teamspeakGroupService = new TeamspeakGroupService(
            _mockRanksContext.Object,
            _mockUnitsContext.Object,
            unitsService,
            _mockTeamspeakManagerService.Object,
            _mockVariablesService.Object,
            _mockTrainingsContext.Object
        );
    }

    private void SetupUnitsContext(List<DomainUnit> units)
    {
        _mockUnitsContext.Setup(x => x.Get()).Returns(units);
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(predicate => units.Where(predicate));
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns<Func<DomainUnit, bool>>(predicate => units.FirstOrDefault(predicate));
    }

    private void SetupRank(string rank, string teamspeakGroup)
    {
        _mockRanksContext.Setup(x => x.GetSingle(rank)).Returns(new DomainRank { Name = rank, TeamspeakGroup = teamspeakGroup });
    }

    private static DomainAccount CreateMember(string id, string rank = "Private", string unitAssignment = null)
    {
        return new DomainAccount
        {
            Id = id,
            MembershipState = MembershipState.Member,
            Rank = rank,
            UnitAssignment = unitAssignment
        };
    }

    [Fact]
    public async Task Should_add_correct_groups_for_candidate()
    {
        var id = ObjectId.GenerateNewId().ToString();
        SetupRank("Candidate", "5");

        await _teamspeakGroupService.UpdateAccountGroups(
            new DomainAccount
            {
                Id = id,
                MembershipState = MembershipState.Confirmed,
                Rank = "Candidate"
            },
            new List<int>(),
            2
        );

        _addedGroups.Should().BeEquivalentTo(new List<int> { 5 });
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_add_correct_groups_for_confirmed_non_member()
    {
        await _teamspeakGroupService.UpdateAccountGroups(new DomainAccount { MembershipState = MembershipState.Confirmed }, new List<int>(), 2);

        _addedGroups.Should().BeEquivalentTo(new List<int>());
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_add_correct_groups_for_discharged()
    {
        await _teamspeakGroupService.UpdateAccountGroups(new DomainAccount { MembershipState = MembershipState.Discharged }, new List<int>(), 2);

        _addedGroups.Should().BeEquivalentTo(new List<int> { 2 });
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_add_correct_groups_for_elcom()
    {
        var id = ObjectId.GenerateNewId().ToString();
        var parentId = ObjectId.GenerateNewId().ToString();
        var parentParentId = ObjectId.GenerateNewId().ToString();
        DomainUnit unit = new()
        {
            Name = "1 Section",
            TeamspeakGroup = "6",
            Members = [id],
            Parent = parentId
        };
        DomainUnit unitParent = new()
        {
            Id = parentId,
            Name = "SFSG",
            TeamspeakGroup = "7",
            Parent = parentParentId
        };
        DomainUnit unitParentParent = new()
        {
            Id = parentParentId,
            Name = "UKSF",
            TeamspeakGroup = "8"
        };
        DomainUnit auxiliaryUnit = new()
        {
            Branch = UnitBranch.Auxiliary,
            Name = "SR1",
            TeamspeakGroup = "9",
            Parent = _elcomUnit.Id,
            Members = [id]
        };
        List<DomainUnit> units = [unit, unitParent, unitParentParent, _elcomUnit, auxiliaryUnit];
        _elcomUnit.Members.Add(id);

        SetupUnitsContext(units);
        SetupRank("Private", "5");

        await _teamspeakGroupService.UpdateAccountGroups(CreateMember(id, unitAssignment: "1 Section"), new List<int>(), 2);

        _addedGroups.Should()
        .BeEquivalentTo(
            new List<int>
            {
                3,
                4,
                5,
                7,
                9
            }
        );
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_add_correct_groups_for_first_root_child()
    {
        var id = ObjectId.GenerateNewId().ToString();
        var rootId = ObjectId.GenerateNewId().ToString();
        DomainUnit rootUnit = new()
        {
            Id = rootId,
            Name = "UKSF",
            TeamspeakGroup = "10",
            Parent = ObjectId.Empty.ToString()
        };
        DomainUnit unit = new()
        {
            Name = "JSFAW",
            TeamspeakGroup = "6",
            Members = [id],
            Parent = rootId
        };
        DomainUnit auxiliaryUnit = new()
        {
            Branch = UnitBranch.Auxiliary,
            Name = "SR1",
            TeamspeakGroup = "9",
            Parent = _elcomUnit.Id,
            Members = [id]
        };
        List<DomainUnit> units = [rootUnit, unit, _elcomUnit, auxiliaryUnit];

        SetupUnitsContext(units);
        SetupRank("Private", "5");

        await _teamspeakGroupService.UpdateAccountGroups(CreateMember(id, unitAssignment: "JSFAW"), new List<int>(), 2);

        _addedGroups.Should()
        .BeEquivalentTo(
            new List<int>
            {
                3,
                5,
                6,
                9
            }
        );
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_add_correct_groups_for_first_root_child_in_elcom()
    {
        var id = ObjectId.GenerateNewId().ToString();
        var rootId = ObjectId.GenerateNewId().ToString();
        DomainUnit rootUnit = new()
        {
            Id = rootId,
            Name = "UKSF",
            TeamspeakGroup = "10",
            Parent = ObjectId.Empty.ToString()
        };
        DomainUnit unit = new()
        {
            Name = "JSFAW",
            TeamspeakGroup = "6",
            Members = [id],
            Parent = rootId
        };
        DomainUnit auxiliaryUnit = new()
        {
            Branch = UnitBranch.Auxiliary,
            Name = "SR1",
            TeamspeakGroup = "9",
            Parent = _elcomUnit.Id,
            Members = [id]
        };
        List<DomainUnit> units = [rootUnit, unit, _elcomUnit, auxiliaryUnit];
        _elcomUnit.Members.Add(id);

        SetupUnitsContext(units);
        SetupRank("Private", "5");

        await _teamspeakGroupService.UpdateAccountGroups(CreateMember(id, unitAssignment: "JSFAW"), new List<int>(), 2);

        _addedGroups.Should()
        .BeEquivalentTo(
            new List<int>
            {
                3,
                5,
                4,
                6,
                9
            }
        );
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_add_correct_groups_for_member()
    {
        var id = ObjectId.GenerateNewId().ToString();
        var parentId = ObjectId.GenerateNewId().ToString();
        var parentParentId = ObjectId.GenerateNewId().ToString();
        DomainUnit unit = new()
        {
            Name = "1 Section",
            TeamspeakGroup = "6",
            Members = [id],
            Parent = parentId
        };
        DomainUnit unitParent = new()
        {
            Id = parentId,
            Name = "SFSG",
            TeamspeakGroup = "7",
            Parent = parentParentId
        };
        DomainUnit unitParentParent = new()
        {
            Id = parentParentId,
            Name = "UKSF",
            TeamspeakGroup = "8"
        };
        DomainUnit auxiliaryUnit = new()
        {
            Branch = UnitBranch.Auxiliary,
            Name = "SR1",
            TeamspeakGroup = "9",
            Parent = _elcomUnit.Id,
            Members = [id]
        };
        List<DomainUnit> units = [unit, unitParent, unitParentParent, _elcomUnit, auxiliaryUnit];

        SetupUnitsContext(units);
        SetupRank("Private", "5");

        await _teamspeakGroupService.UpdateAccountGroups(CreateMember(id, unitAssignment: "1 Section"), new List<int>(), 2);

        _addedGroups.Should()
        .BeEquivalentTo(
            new List<int>
            {
                3,
                5,
                6,
                7,
                9
            }
        );
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_add_correct_groups_for_member_with_gaps_in_parents()
    {
        var id = ObjectId.GenerateNewId().ToString();
        var parentId = ObjectId.GenerateNewId().ToString();
        var parentParentId = ObjectId.GenerateNewId().ToString();
        var parentParentParentId = ObjectId.GenerateNewId().ToString();
        DomainUnit unit = new()
        {
            Name = "1 Section",
            Members = [id],
            Parent = parentId
        };
        DomainUnit unitParent = new()
        {
            Id = parentId,
            Name = "1 Platoon",
            TeamspeakGroup = "7",
            Parent = parentParentId
        };
        DomainUnit unitParentParent = new()
        {
            Id = parentParentId,
            Name = "A Company",
            Parent = parentParentParentId
        };
        DomainUnit unitParentParentParent = new()
        {
            Id = parentParentParentId,
            Name = "SFSG",
            TeamspeakGroup = "8"
        };
        List<DomainUnit> units = [unit, unitParent, unitParentParent, unitParentParentParent, _elcomUnit];

        SetupUnitsContext(units);
        SetupRank("Private", "5");

        await _teamspeakGroupService.UpdateAccountGroups(CreateMember(id, unitAssignment: "1 Section"), new List<int>(), 2);

        _addedGroups.Should()
        .BeEquivalentTo(
            new List<int>
            {
                3,
                5,
                7,
                8
            }
        );
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_add_correct_groups_for_non_member()
    {
        await _teamspeakGroupService.UpdateAccountGroups(new DomainAccount { MembershipState = MembershipState.Unconfirmed }, new List<int>(), 2);

        _addedGroups.Should().BeEquivalentTo(new List<int> { 1 });
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_add_correct_groups_for_non_member_with_no_account()
    {
        await _teamspeakGroupService.UpdateAccountGroups(null, new List<int>(), 2);

        _addedGroups.Should().BeEquivalentTo(new List<int> { 1 });
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_add_correct_groups_for_secondary_units()
    {
        var id = ObjectId.GenerateNewId().ToString();
        DomainUnit unit = new()
        {
            Name = "Combat Unit",
            TeamspeakGroup = "6",
            Members = [id],
            Parent = ObjectId.GenerateNewId().ToString(),
            Branch = UnitBranch.Combat
        };
        DomainUnit auxiliaryUnit = new()
        {
            Branch = UnitBranch.Auxiliary,
            Name = "Auxiliary Unit",
            TeamspeakGroup = "9",
            Parent = _elcomUnit.Id,
            Members = [id]
        };
        DomainUnit secondaryUnit = new()
        {
            Branch = UnitBranch.Secondary,
            Name = "Secondary Unit",
            TeamspeakGroup = "11",
            Parent = ObjectId.GenerateNewId().ToString(),
            Members = [id]
        };
        List<DomainUnit> units = [unit, _elcomUnit, auxiliaryUnit, secondaryUnit];

        SetupUnitsContext(units);
        SetupRank("Private", "5");

        await _teamspeakGroupService.UpdateAccountGroups(CreateMember(id, unitAssignment: "Combat Unit"), new List<int>(), 2);

        _addedGroups.Should().Contain(9);
        _addedGroups.Should().NotContain(11);
        _addedGroups.Should().Contain(3);
        _addedGroups.Should().Contain(5);
        _addedGroups.Should().Contain(6);
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_add_correct_groups_for_stratcom()
    {
        var id = ObjectId.GenerateNewId().ToString();
        DomainUnit rootUnit = new()
        {
            Name = "UKSF",
            TeamspeakGroup = "10",
            Members = [id],
            Parent = ObjectId.Empty.ToString()
        };
        DomainUnit auxiliaryUnit = new()
        {
            Branch = UnitBranch.Auxiliary,
            Name = "SR1",
            TeamspeakGroup = "9",
            Parent = _elcomUnit.Id,
            Members = [id]
        };
        List<DomainUnit> units = [rootUnit, _elcomUnit, auxiliaryUnit];
        _elcomUnit.Members.Add(id);

        SetupUnitsContext(units);
        SetupRank("Private", "5");

        await _teamspeakGroupService.UpdateAccountGroups(CreateMember(id, unitAssignment: "UKSF"), new List<int>(), 2);

        _addedGroups.Should()
        .BeEquivalentTo(
            new List<int>
            {
                3,
                4,
                5,
                10,
                9
            }
        );
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_add_training_groups()
    {
        var id = ObjectId.GenerateNewId().ToString();
        var trainingId1 = ObjectId.GenerateNewId().ToString();
        var trainingId2 = ObjectId.GenerateNewId().ToString();
        DomainUnit unit = new()
        {
            Name = "Combat Unit",
            TeamspeakGroup = "6",
            Members = [id],
            Parent = ObjectId.GenerateNewId().ToString(),
            Branch = UnitBranch.Combat
        };
        List<DomainUnit> units = [unit, _elcomUnit];

        _mockTrainingsContext.Setup(x => x.Get())
                             .Returns(
                                 new List<DomainTraining>
                                 {
                                     new() { Id = trainingId1, TeamspeakGroup = "12" }, new() { Id = trainingId2, TeamspeakGroup = "13" }
                                 }
                             );
        SetupUnitsContext(units);
        SetupRank("Private", "5");

        var account = CreateMember(id, unitAssignment: "Combat Unit");
        account.Trainings = [trainingId1, trainingId2];
        await _teamspeakGroupService.UpdateAccountGroups(account, new List<int>(), 2);

        _addedGroups.Should().Contain(12);
        _addedGroups.Should().Contain(13);
        _addedGroups.Should().Contain(3);
        _addedGroups.Should().Contain(5);
        _addedGroups.Should().Contain(6);
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_fallback_to_parent_group_when_unit_teamspeak_group_is_zero()
    {
        var id = ObjectId.GenerateNewId().ToString();
        var parentId = ObjectId.GenerateNewId().ToString();
        DomainUnit unit = new()
        {
            Name = "1 Section",
            TeamspeakGroup = "0",
            Members = [id],
            Parent = parentId
        };
        DomainUnit unitParent = new()
        {
            Id = parentId,
            Name = "SFSG",
            TeamspeakGroup = "7"
        };
        List<DomainUnit> units = [unit, unitParent, _elcomUnit];

        SetupUnitsContext(units);
        SetupRank("Private", "5");

        await _teamspeakGroupService.UpdateAccountGroups(CreateMember(id, unitAssignment: "1 Section"), new List<int>(), 2);

        _addedGroups.Should().Contain(3);
        _addedGroups.Should().Contain(5);
        _addedGroups.Should().Contain(7);
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_fallback_to_unit_group_when_no_parent_with_teamspeak_group_exists()
    {
        var id = ObjectId.GenerateNewId().ToString();
        var parentId = ObjectId.GenerateNewId().ToString();
        DomainUnit unit = new()
        {
            Name = "1 Section",
            TeamspeakGroup = "6",
            Members = [id],
            Parent = parentId
        };
        DomainUnit unitParent = new()
        {
            Id = parentId,
            Name = "SFSG",
            TeamspeakGroup = "",
            Parent = ObjectId.Empty.ToString()
        };
        List<DomainUnit> units = [unit, unitParent, _elcomUnit];

        SetupUnitsContext(units);
        SetupRank("Private", "5");

        await _teamspeakGroupService.UpdateAccountGroups(CreateMember(id, unitAssignment: "1 Section"), new List<int>(), 2);

        _addedGroups.Should().Contain(3);
        _addedGroups.Should().Contain(5);
        _addedGroups.Should().Contain(6);
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_handle_account_with_no_trainings()
    {
        var id = ObjectId.GenerateNewId().ToString();
        DomainUnit unit = new()
        {
            Name = "Combat Unit",
            TeamspeakGroup = "6",
            Members = [id],
            Parent = ObjectId.GenerateNewId().ToString(),
            Branch = UnitBranch.Combat
        };
        List<DomainUnit> units = [unit, _elcomUnit];

        _mockTrainingsContext.Setup(x => x.Get()).Returns(new List<DomainTraining>());
        SetupUnitsContext(units);
        SetupRank("Private", "5");

        var account = CreateMember(id, unitAssignment: "Combat Unit");
        account.Trainings = [];
        await _teamspeakGroupService.UpdateAccountGroups(account, new List<int>(), 2);

        _addedGroups.Should().Contain(3);
        _addedGroups.Should().Contain(5);
        _addedGroups.Should().Contain(6);
        _addedGroups.Should().NotContain(12);
        _addedGroups.Should().NotContain(13);
        _removedGroups.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Should_not_add_rank_group_when_rank_is_null_or_empty(string rank)
    {
        var id = ObjectId.GenerateNewId().ToString();
        DomainUnit unit = new()
        {
            Name = "1 Section",
            TeamspeakGroup = "6",
            Members = [id],
            Parent = ObjectId.GenerateNewId().ToString()
        };
        List<DomainUnit> units = [unit, _elcomUnit];

        SetupUnitsContext(units);

        await _teamspeakGroupService.UpdateAccountGroups(CreateMember(id, rank: rank, unitAssignment: "1 Section"), new List<int>(), 2);

        _addedGroups.Should().Contain(3);
        _addedGroups.Should().Contain(6);
        _addedGroups.Should().NotContain(5);
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_only_add_groups_if_not_set()
    {
        var id = ObjectId.GenerateNewId().ToString();
        var parentId = ObjectId.GenerateNewId().ToString();
        var parentParentId = ObjectId.GenerateNewId().ToString();
        DomainUnit unit = new()
        {
            Name = "1 Section",
            TeamspeakGroup = "6",
            Members = [id],
            Parent = parentId
        };
        DomainUnit unitParent = new()
        {
            Id = parentId,
            Name = "SFSG",
            TeamspeakGroup = "7",
            Parent = parentParentId
        };
        DomainUnit unitParentParent = new()
        {
            Id = parentParentId,
            Name = "UKSF",
            TeamspeakGroup = "8"
        };
        DomainUnit auxiliaryUnit = new()
        {
            Branch = UnitBranch.Auxiliary,
            Name = "SR1",
            TeamspeakGroup = "9",
            Parent = _elcomUnit.Id,
            Members = [id]
        };
        List<DomainUnit> units = [unit, unitParent, unitParentParent, _elcomUnit, auxiliaryUnit];

        SetupUnitsContext(units);
        SetupRank("Private", "5");

        await _teamspeakGroupService.UpdateAccountGroups(CreateMember(id, unitAssignment: "1 Section"), new List<int> { 3, 5 }, 2);

        _addedGroups.Should()
        .BeEquivalentTo(
            new List<int>
            {
                6,
                7,
                9
            }
        );
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_remove_correct_groups()
    {
        var id = ObjectId.GenerateNewId().ToString();
        var parentId = ObjectId.GenerateNewId().ToString();
        var parentParentId = ObjectId.GenerateNewId().ToString();
        DomainUnit unit = new()
        {
            Name = "1 Section",
            TeamspeakGroup = "6",
            Members = [id],
            Parent = parentId
        };
        DomainUnit unitParent = new()
        {
            Id = parentId,
            Name = "SFSG",
            TeamspeakGroup = "7",
            Parent = parentParentId
        };
        DomainUnit unitParentParent = new()
        {
            Id = parentParentId,
            Name = "UKSF",
            TeamspeakGroup = "8"
        };
        DomainUnit auxiliaryUnit = new()
        {
            Branch = UnitBranch.Auxiliary,
            Name = "SR1",
            TeamspeakGroup = "9",
            Parent = _elcomUnit.Id,
            Members = [id]
        };
        List<DomainUnit> units = [unit, unitParent, unitParentParent, _elcomUnit, auxiliaryUnit];

        SetupUnitsContext(units);
        SetupRank("Private", "5");

        await _teamspeakGroupService.UpdateAccountGroups(CreateMember(id, unitAssignment: "1 Section"), new List<int> { 1, 10 }, 2);

        _addedGroups.Should()
        .BeEquivalentTo(
            new List<int>
            {
                3,
                5,
                6,
                7,
                9
            }
        );
        _removedGroups.Should().BeEquivalentTo(new List<int> { 1, 10 });
    }

    [Fact]
    public async Task Should_remove_groups()
    {
        await _teamspeakGroupService.UpdateAccountGroups(
            null,
            new List<int>
            {
                1,
                3,
                4
            },
            2
        );

        _addedGroups.Should().BeEmpty();
        _removedGroups.Should().BeEquivalentTo(new List<int> { 3, 4 });
    }

    [Fact]
    public async Task Should_remove_groups_except_blacklisted()
    {
        await _teamspeakGroupService.UpdateAccountGroups(
            null,
            new List<int>
            {
                1,
                3,
                4,
                99,
                100
            },
            2
        );

        _addedGroups.Should().BeEmpty();
        _removedGroups.Should().BeEquivalentTo(new List<int> { 3, 4 });
    }

    [Fact]
    public async Task Should_skip_auxiliary_units_without_teamspeak_groups()
    {
        var id = ObjectId.GenerateNewId().ToString();
        DomainUnit unit = new()
        {
            Name = "Combat Unit",
            TeamspeakGroup = "6",
            Members = [id],
            Parent = ObjectId.GenerateNewId().ToString(),
            Branch = UnitBranch.Combat
        };
        DomainUnit auxiliaryUnitWithGroup = new()
        {
            Branch = UnitBranch.Auxiliary,
            Name = "Auxiliary Unit With Group",
            TeamspeakGroup = "9",
            Parent = _elcomUnit.Id,
            Members = [id]
        };
        DomainUnit auxiliaryUnitWithoutGroup = new()
        {
            Branch = UnitBranch.Auxiliary,
            Name = "Auxiliary Unit Without Group",
            TeamspeakGroup = "",
            Parent = _elcomUnit.Id,
            Members = [id]
        };
        List<DomainUnit> units = [unit, _elcomUnit, auxiliaryUnitWithGroup, auxiliaryUnitWithoutGroup];

        SetupUnitsContext(units);
        SetupRank("Private", "5");

        await _teamspeakGroupService.UpdateAccountGroups(CreateMember(id, unitAssignment: "Combat Unit"), new List<int>(), 2);

        _addedGroups.Should().Contain(9);
        _addedGroups.Should().NotContain(0);
        _addedGroups.Should().Contain(3);
        _addedGroups.Should().Contain(5);
        _addedGroups.Should().Contain(6);
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_skip_parent_groups_already_in_assigned_groups()
    {
        var id = ObjectId.GenerateNewId().ToString();
        var parentId = ObjectId.GenerateNewId().ToString();
        var parentParentId = ObjectId.GenerateNewId().ToString();
        DomainUnit unit = new()
        {
            Name = "1 Section",
            TeamspeakGroup = "6",
            Members = [id],
            Parent = parentId
        };
        DomainUnit unitParent = new()
        {
            Id = parentId,
            Name = "SFSG",
            TeamspeakGroup = "6",
            Parent = parentParentId
        };
        DomainUnit unitParentParent = new()
        {
            Id = parentParentId,
            Name = "UKSF",
            TeamspeakGroup = "8"
        };
        List<DomainUnit> units = [unit, unitParent, unitParentParent, _elcomUnit];

        var mockUnitsService = new Mock<IUnitsService>();
        mockUnitsService.Setup(x => x.GetParents(It.IsAny<DomainUnit>()))
        .Returns(
            new List<DomainUnit>
            {
                unit,
                unitParent,
                unitParentParent
            }
        );
        mockUnitsService.Setup(x => x.GetAuxiliaryRoot()).Returns(_elcomUnit);

        var teamspeakGroupService = new TeamspeakGroupService(
            _mockRanksContext.Object,
            _mockUnitsContext.Object,
            mockUnitsService.Object,
            _mockTeamspeakManagerService.Object,
            _mockVariablesService.Object,
            _mockTrainingsContext.Object
        );

        SetupUnitsContext(units);
        SetupRank("Private", "5");

        await teamspeakGroupService.UpdateAccountGroups(CreateMember(id, unitAssignment: "1 Section"), new List<int>(), 2);

        _addedGroups.Should().Contain(3);
        _addedGroups.Should().Contain(5);
        _addedGroups.Should().Contain(6);
        _addedGroups.Should().Contain(8);
        _removedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_skip_training_groups_that_do_not_exist()
    {
        var id = ObjectId.GenerateNewId().ToString();
        var trainingId1 = ObjectId.GenerateNewId().ToString();
        var trainingId2 = ObjectId.GenerateNewId().ToString();
        DomainUnit unit = new()
        {
            Name = "Combat Unit",
            TeamspeakGroup = "6",
            Members = [id],
            Parent = ObjectId.GenerateNewId().ToString(),
            Branch = UnitBranch.Combat
        };
        List<DomainUnit> units = [unit, _elcomUnit];

        _mockTrainingsContext.Setup(x => x.Get()).Returns(new List<DomainTraining> { new() { Id = trainingId1, TeamspeakGroup = "12" } });
        SetupUnitsContext(units);
        SetupRank("Private", "5");

        var account = CreateMember(id, unitAssignment: "Combat Unit");
        account.Trainings = [trainingId1, trainingId2];
        await _teamspeakGroupService.UpdateAccountGroups(account, new List<int>(), 2);

        _addedGroups.Should().Contain(12);
        _addedGroups.Should().NotContain(0);
        _addedGroups.Should().Contain(3);
        _addedGroups.Should().Contain(5);
        _addedGroups.Should().Contain(6);
        _removedGroups.Should().BeEmpty();
    }
}
