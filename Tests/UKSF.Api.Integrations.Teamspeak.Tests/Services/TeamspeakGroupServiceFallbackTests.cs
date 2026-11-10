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

public class TeamspeakGroupServiceFallbackTests : TeamspeakGroupServiceTestBase
{
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
}
