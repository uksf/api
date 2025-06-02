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
            new Mock<ITrainingsContext>().Object
        );
    }

    [Fact]
    public async Task Should_add_correct_groups_for_candidate()
    {
        var id = ObjectId.GenerateNewId().ToString();

        _mockRanksContext.Setup(x => x.GetSingle("Candidate")).Returns(new DomainRank { Name = "Candidate", TeamspeakGroup = "5" });

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

        _mockUnitsContext.Setup(x => x.Get()).Returns(units);
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(predicate => units.Where(predicate));
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns<Func<DomainUnit, bool>>(predicate => units.FirstOrDefault(predicate));
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(new DomainRank { Name = "Private", TeamspeakGroup = "5" });

        await _teamspeakGroupService.UpdateAccountGroups(
            new DomainAccount
            {
                Id = id,
                MembershipState = MembershipState.Member,
                Rank = "Private",
                UnitAssignment = "1 Section"
            },
            new List<int>(),
            2
        );

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

        _mockUnitsContext.Setup(x => x.Get()).Returns(units);
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(predicate => units.Where(predicate));
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns<Func<DomainUnit, bool>>(predicate => units.FirstOrDefault(predicate));
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(new DomainRank { Name = "Private", TeamspeakGroup = "5" });

        await _teamspeakGroupService.UpdateAccountGroups(
            new DomainAccount
            {
                Id = id,
                MembershipState = MembershipState.Member,
                Rank = "Private",
                UnitAssignment = "JSFAW"
            },
            new List<int>(),
            2
        );

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

        _mockUnitsContext.Setup(x => x.Get()).Returns(units);
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(predicate => units.Where(predicate));
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns<Func<DomainUnit, bool>>(predicate => units.FirstOrDefault(predicate));
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(new DomainRank { Name = "Private", TeamspeakGroup = "5" });

        await _teamspeakGroupService.UpdateAccountGroups(
            new DomainAccount
            {
                Id = id,
                MembershipState = MembershipState.Member,
                Rank = "Private",
                UnitAssignment = "JSFAW"
            },
            new List<int>(),
            2
        );

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

        _mockUnitsContext.Setup(x => x.Get()).Returns(units);
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(predicate => units.Where(predicate));
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns<Func<DomainUnit, bool>>(predicate => units.FirstOrDefault(predicate));
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(new DomainRank { Name = "Private", TeamspeakGroup = "5" });

        await _teamspeakGroupService.UpdateAccountGroups(
            new DomainAccount
            {
                Id = id,
                MembershipState = MembershipState.Member,
                Rank = "Private",
                UnitAssignment = "1 Section"
            },
            new List<int>(),
            2
        );

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

        _mockUnitsContext.Setup(x => x.Get()).Returns(units);
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(predicate => units.Where(predicate));
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns<Func<DomainUnit, bool>>(predicate => units.FirstOrDefault(predicate));
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(new DomainRank { Name = "Private", TeamspeakGroup = "5" });

        await _teamspeakGroupService.UpdateAccountGroups(
            new DomainAccount
            {
                Id = id,
                MembershipState = MembershipState.Member,
                Rank = "Private",
                UnitAssignment = "1 Section"
            },
            new List<int>(),
            2
        );

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

        _mockUnitsContext.Setup(x => x.Get()).Returns(units);
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(predicate => units.Where(predicate));
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns<Func<DomainUnit, bool>>(predicate => units.FirstOrDefault(predicate));
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(new DomainRank { Name = "Private", TeamspeakGroup = "5" });

        await _teamspeakGroupService.UpdateAccountGroups(
            new DomainAccount
            {
                Id = id,
                MembershipState = MembershipState.Member,
                Rank = "Private",
                UnitAssignment = "UKSF"
            },
            new List<int>(),
            2
        );

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

        _mockUnitsContext.Setup(x => x.Get()).Returns(units);
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(predicate => units.Where(predicate));
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns<Func<DomainUnit, bool>>(predicate => units.FirstOrDefault(predicate));
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(new DomainRank { Name = "Private", TeamspeakGroup = "5" });

        await _teamspeakGroupService.UpdateAccountGroups(
            new DomainAccount
            {
                Id = id,
                MembershipState = MembershipState.Member,
                Rank = "Private",
                UnitAssignment = "1 Section"
            },
            new List<int> { 3, 5 },
            2
        );

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

        _mockUnitsContext.Setup(x => x.Get()).Returns(units);
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(predicate => units.Where(predicate));
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns<Func<DomainUnit, bool>>(predicate => units.FirstOrDefault(predicate));
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(new DomainRank { Name = "Private", TeamspeakGroup = "5" });

        await _teamspeakGroupService.UpdateAccountGroups(
            new DomainAccount
            {
                Id = id,
                MembershipState = MembershipState.Member,
                Rank = "Private",
                UnitAssignment = "1 Section"
            },
            new List<int> { 1, 10 },
            2
        );

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
    public async Task Should_add_correct_groups_for_secondary_units()
    {
        var id = ObjectId.GenerateNewId().ToString();
        var parentId = ObjectId.GenerateNewId().ToString();
        DomainUnit unit = new()
        {
            Name = "Combat Unit",
            TeamspeakGroup = "6",
            Members = [id],
            Parent = parentId,
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

        _mockUnitsContext.Setup(x => x.Get()).Returns(units);
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(predicate => units.Where(predicate));
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns<Func<DomainUnit, bool>>(predicate => units.FirstOrDefault(predicate));
        _mockRanksContext.Setup(x => x.GetSingle("Private")).Returns(new DomainRank { Name = "Private", TeamspeakGroup = "5" });

        await _teamspeakGroupService.UpdateAccountGroups(
            new DomainAccount
            {
                Id = id,
                MembershipState = MembershipState.Member,
                Rank = "Private",
                UnitAssignment = "Combat Unit"
            },
            new List<int>(),
            2
        );

        // Should include both auxiliary (9) and secondary (11) unit groups
        _addedGroups.Should().Contain(new List<int> { 9, 11 });
        _addedGroups.Should().Contain(3); // Root group
        _addedGroups.Should().Contain(5); // Rank group
        _removedGroups.Should().BeEmpty();
    }
}
