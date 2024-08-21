using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Tests.Unit.Services.Personnel;

public class RolesServiceTests
{
    private readonly Mock<IRolesContext> _mockRolesDataService;
    private readonly RolesService _rolesService;

    public RolesServiceTests()
    {
        _mockRolesDataService = new Mock<IRolesContext>();
        _rolesService = new RolesService(_mockRolesDataService.Object);
    }

    [Fact]
    public void ShouldReturnNullWhenNoUnitRoleFound()
    {
        _mockRolesDataService.Setup(x => x.GetSingle(It.IsAny<Func<DomainRole, bool>>())).Returns<Func<DomainRole, bool>>(null);

        var subject = _rolesService.GetUnitRoleByOrder(2);

        subject.Should().BeNull();
    }

    [Fact]
    public void ShouldReturnZeroForSortWhenRanksAreNull()
    {
        _mockRolesDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(null);

        var subject = _rolesService.Sort("Trainee", "Rifleman");

        subject.Should().Be(0);
    }

    [Theory]
    [InlineData("Trainee", "Rifleman", 1)]
    [InlineData("Rifleman", "Trainee", -1)]
    [InlineData("Rifleman", "Rifleman", 0)]
    public void ShouldGetCorrectSortValueByName(string nameA, string nameB, int expected)
    {
        DomainRole role1 = new() { Name = "Rifleman", Order = 0 };
        DomainRole role2 = new() { Name = "Trainee", Order = 1 };
        List<DomainRole> mockCollection = [role1, role2];

        _mockRolesDataService.Setup(x => x.Get()).Returns(mockCollection);
        _mockRolesDataService.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(x => mockCollection.FirstOrDefault(y => y.Name == x));

        var subject = _rolesService.Sort(nameA, nameB);

        subject.Should().Be(expected);
    }

    [Theory]
    [InlineData(3, "Trainee")]
    [InlineData(0, "Marksman")]
    public void ShouldGetUnitRoleByOrder(int order, string expected)
    {
        DomainRole role1 = new()
        {
            Name = "Rifleman",
            Order = 0,
            RoleType = RoleType.Individual
        };
        DomainRole role2 = new()
        {
            Name = "Gunner",
            Order = 3,
            RoleType = RoleType.Individual
        };
        DomainRole role3 = new()
        {
            Name = "Marksman",
            Order = 0,
            RoleType = RoleType.Unit
        };
        DomainRole role4 = new()
        {
            Name = "Trainee",
            Order = 3,
            RoleType = RoleType.Unit
        };
        DomainRole role5 = new()
        {
            Name = "Gunner",
            Order = 2,
            RoleType = RoleType.Individual
        };
        List<DomainRole> mockCollection = [role1, role2, role3, role4, role5];

        _mockRolesDataService.Setup(x => x.Get()).Returns(mockCollection);
        _mockRolesDataService.Setup(x => x.GetSingle(It.IsAny<Func<DomainRole, bool>>()))
                             .Returns<Func<DomainRole, bool>>(x => mockCollection.FirstOrDefault(x));

        var subject = _rolesService.GetUnitRoleByOrder(order);

        subject.Name.Should().Be(expected);
    }
}
