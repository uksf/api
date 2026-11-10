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

public class TeamspeakGroupServiceSkipTests : TeamspeakGroupServiceTestBase
{
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

        var teamspeakGroupService = CreateService(mockUnitsService.Object);

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
