using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Integrations.Teamspeak.Tests.Services;

public class TeamspeakGroupServiceMembershipTests : TeamspeakGroupServiceTestBase
{
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
}
