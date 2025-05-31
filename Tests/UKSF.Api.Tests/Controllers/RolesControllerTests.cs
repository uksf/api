using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Controllers;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Tests.Controllers;

public class RolesControllerTests
{
    private readonly Mock<IRolesContext> _mockRolesContext;
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly Mock<IAssignmentService> _mockAssignmentService;
    private readonly Mock<INotificationsService> _mockNotificationsService;
    private readonly Mock<IUksfLogger> _mockLogger;
    private readonly RolesController _controller;

    public RolesControllerTests()
    {
        _mockRolesContext = new Mock<IRolesContext>();
        _mockAccountContext = new Mock<IAccountContext>();
        _mockAssignmentService = new Mock<IAssignmentService>();
        _mockNotificationsService = new Mock<INotificationsService>();
        _mockLogger = new Mock<IUksfLogger>();

        _controller = new RolesController(
            _mockRolesContext.Object,
            _mockAccountContext.Object,
            _mockAssignmentService.Object,
            _mockNotificationsService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public void GetRoles_Should_Return_All_Roles_When_No_Id_Provided()
    {
        // Arrange
        var roles = new List<DomainRole>
        {
            new() { Id = "1", Name = "Rifleman" },
            new() { Id = "2", Name = "Marksman" },
            new() { Id = "3", Name = "Medic" }
        };

        _mockRolesContext.Setup(x => x.Get()).Returns(roles);

        // Act
        var result = _controller.GetRoles();

        // Assert
        result.Should().NotBeNull();
        result.Roles.Should().HaveCount(3);
        result.Roles.Should().Contain(x => x.Name == "Rifleman");
        result.Roles.Should().Contain(x => x.Name == "Marksman");
        result.Roles.Should().Contain(x => x.Name == "Medic");
    }

    [Fact]
    public void GetRoles_Should_Return_Roles_Excluding_Account_Role_When_Id_Provided()
    {
        // Arrange
        var accountId = "account123";
        var account = new DomainAccount { Id = accountId, RoleAssignment = "Rifleman" };
        var filteredRoles = new List<DomainRole> { new() { Id = "2", Name = "Marksman" }, new() { Id = "3", Name = "Medic" } };

        _mockAccountContext.Setup(x => x.GetSingle(accountId)).Returns(account);
        _mockRolesContext.Setup(x => x.Get(It.IsAny<System.Func<DomainRole, bool>>())).Returns(filteredRoles);

        // Act
        var result = _controller.GetRoles(accountId);

        // Assert
        result.Should().NotBeNull();
        result.Roles.Should().HaveCount(2);
        result.Roles.Should().NotContain(x => x.Name == "Rifleman");
        result.Roles.Should().Contain(x => x.Name == "Marksman");
        result.Roles.Should().Contain(x => x.Name == "Medic");
    }

    [Fact]
    public void CheckRole_Should_Return_Null_When_Check_Is_Empty()
    {
        // Act
        var result = _controller.CheckRole("");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CheckRole_Should_Return_Role_With_Same_Name_When_No_Role_Provided()
    {
        // Arrange
        var roleName = "Rifleman";
        var expectedRole = new DomainRole { Id = "1", Name = roleName };

        _mockRolesContext.Setup(x => x.GetSingle(It.IsAny<System.Func<DomainRole, bool>>())).Returns(expectedRole);

        // Act
        var result = _controller.CheckRole(roleName);

        // Assert
        result.Should().Be(expectedRole);
    }

    [Fact]
    public void CheckRole_Should_Return_Role_With_Same_Name_Excluding_Given_Role_When_Role_Provided()
    {
        // Arrange
        var roleName = "Rifleman";
        var existingRole = new DomainRole { Id = "1", Name = "Marksman" };
        var expectedRole = new DomainRole { Id = "2", Name = roleName };

        _mockRolesContext.Setup(x => x.GetSingle(It.IsAny<System.Func<DomainRole, bool>>())).Returns(expectedRole);

        // Act
        var result = _controller.CheckRole(roleName, existingRole);

        // Assert
        result.Should().Be(expectedRole);
    }

    [Fact]
    public async Task AddRole_Should_Add_Role_And_Return_All_Roles()
    {
        // Arrange
        var newRole = new DomainRole { Id = "1", Name = "Engineer" };
        var allRoles = new List<DomainRole>
        {
            newRole,
            new() { Id = "2", Name = "Rifleman" },
            new() { Id = "3", Name = "Marksman" }
        };

        _mockRolesContext.Setup(x => x.Get()).Returns(allRoles);

        // Act
        var result = await _controller.AddRole(newRole);

        // Assert
        result.Should().NotBeNull();
        result.Roles.Should().HaveCount(3);
        result.Roles.Should().Contain(x => x.Name == "Engineer");

        _mockRolesContext.Verify(x => x.Add(newRole), Times.Once);
    }

    [Fact]
    public async Task EditRole_Should_Update_Role_And_Account_Assignments()
    {
        // Arrange
        var roleId = "role123";
        var oldRole = new DomainRole { Id = roleId, Name = "OldRoleName" };
        var updatedRole = new DomainRole { Id = roleId, Name = "NewRoleName" };
        var accounts = new List<DomainAccount>
        {
            new() { Id = "account1", RoleAssignment = "OldRoleName" }, new() { Id = "account2", RoleAssignment = "OldRoleName" }
        };
        var allRoles = new List<DomainRole> { updatedRole };

        _mockRolesContext.Setup(x => x.GetSingle(It.IsAny<System.Func<DomainRole, bool>>())).Returns(oldRole);
        _mockAccountContext.Setup(x => x.Get(It.IsAny<System.Func<DomainAccount, bool>>())).Returns(accounts);
        _mockRolesContext.Setup(x => x.Get()).Returns(allRoles);

        // Act
        var result = await _controller.EditRole(updatedRole);

        // Assert
        result.Should().NotBeNull();
        result.Roles.Should().HaveCount(1);
        result.Roles.Should().Contain(x => x.Name == "NewRoleName");
    }

    [Fact]
    public async Task DeleteRole_Should_Delete_Role_And_Update_Account_Assignments()
    {
        // Arrange
        var roleId = "role123";
        var roleToDelete = new DomainRole { Id = roleId, Name = "RoleToDelete" };
        var accounts = new List<DomainAccount>
        {
            new() { Id = "account1", RoleAssignment = "RoleToDelete" }, new() { Id = "account2", RoleAssignment = "RoleToDelete" }
        };
        var remainingRoles = new List<DomainRole>();

        _mockRolesContext.Setup(x => x.GetSingle(It.IsAny<System.Func<DomainRole, bool>>())).Returns(roleToDelete);
        _mockAccountContext.Setup(x => x.Get(It.IsAny<System.Func<DomainAccount, bool>>())).Returns(accounts);
        _mockRolesContext.Setup(x => x.Get()).Returns(remainingRoles);

        // Act
        var result = await _controller.DeleteRole(roleId);

        // Assert
        result.Should().NotBeNull();
        result.Roles.Should().BeEmpty();

        _mockRolesContext.Verify(x => x.Delete(roleId), Times.Once);
    }
}
