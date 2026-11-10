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

public class TeamspeakGroupServiceUnitTests : TeamspeakGroupServiceTestBase
{
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
}
