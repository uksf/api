using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services.Personnel;

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
    [InlineData(0, "1iC")]
    [InlineData(1, "2iC")]
    [InlineData(2, "3iC")]
    [InlineData(3, "NCOiC")]
    [InlineData(999, "")]
    public void GetUnitRoleNameByOrder_Should_Return_Correct_Role_Name(int order, string expected)
    {
        // Act
        var result = _rolesService.GetUnitRoleNameByOrder(order);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1iC", 0)]
    [InlineData("2iC", 1)]
    [InlineData("3iC", 2)]
    [InlineData("NCOiC", 3)]
    [InlineData("NonExistent", 0)] // Default value for key not found
    public void GetUnitRoleOrderByName_Should_Return_Correct_Order(string roleName, int expected)
    {
        // Act
        var result = _rolesService.GetUnitRoleOrderByName(roleName);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetCommanderRoleName_Should_Return_1iC()
    {
        // Act
        var result = _rolesService.GetCommanderRoleName();

        // Assert
        result.Should().Be("1iC");
    }
}
