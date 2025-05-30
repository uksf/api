using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
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
    private readonly Mock<IUnitsContext> _mockUnitsContext;
    private readonly Mock<IRolesContext> _mockRolesContext;
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly Mock<IAssignmentService> _mockAssignmentService;
    private readonly Mock<INotificationsService> _mockNotificationsService;
    private readonly Mock<IUksfLogger> _mockLogger;
    private readonly RolesController _controller;

    private readonly string _unitId = "unit123";
    private readonly string _memberId = "member123";
    private readonly string _otherMemberId = "other-member";

    public RolesControllerTests()
    {
        _mockUnitsContext = new Mock<IUnitsContext>();
        _mockRolesContext = new Mock<IRolesContext>();
        _mockAccountContext = new Mock<IAccountContext>();
        _mockAssignmentService = new Mock<IAssignmentService>();
        _mockNotificationsService = new Mock<INotificationsService>();
        _mockLogger = new Mock<IUksfLogger>();

        _controller = new RolesController(
            _mockUnitsContext.Object,
            _mockRolesContext.Object,
            _mockAccountContext.Object,
            _mockAssignmentService.Object,
            _mockNotificationsService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public void GetRoles_Should_Return_Only_Empty_Positions_When_Member_Has_Position()
    {
        // Arrange
        var unit = new DomainUnit
        {
            Id = _unitId,
            Name = "Test Unit",
            ChainOfCommand = new ChainOfCommand
            {
                First = _memberId, // Member holds 1iC position
                Second = _otherMemberId, // Other member holds 2iC position
                // ThreeIC and NCOIC are empty
            }
        };

        _mockUnitsContext.Setup(x => x.GetSingle(_unitId)).Returns(unit);

        // Act
        var result = _controller.GetRoles(_memberId, _unitId) as RolesDataset;

        // Assert
        result.Should().NotBeNull();
        result.UnitRoles.Should().NotBeNull();

        // Should only return empty positions (3iC and NCOiC)
        var availablePositions = result.UnitRoles.Select(x => x.Name).ToList();
        availablePositions.Should().Contain("3iC");
        availablePositions.Should().Contain("NCOiC");

        // Should NOT return positions that are already filled (1iC, 2iC)
        availablePositions.Should().NotContain("1iC"); // Member's current position
        availablePositions.Should().NotContain("2iC"); // Other member's position
    }

    [Fact]
    public void GetRoles_Should_Return_All_Empty_Positions_When_Member_Has_No_Position()
    {
        // Arrange
        var unit = new DomainUnit
        {
            Id = _unitId,
            Name = "Test Unit",
            ChainOfCommand = new ChainOfCommand
            {
                First = _otherMemberId, // Other member holds 1iC position
                // All other positions are empty
            }
        };

        _mockUnitsContext.Setup(x => x.GetSingle(_unitId)).Returns(unit);

        // Act
        var result = _controller.GetRoles(_memberId, _unitId) as RolesDataset;

        // Assert
        result.Should().NotBeNull();
        result.UnitRoles.Should().NotBeNull();

        // Should return all empty positions (2iC, 3iC, NCOiC)
        var availablePositions = result.UnitRoles.Select(x => x.Name).ToList();
        availablePositions.Should().Contain("2iC");
        availablePositions.Should().Contain("3iC");
        availablePositions.Should().Contain("NCOiC");

        // Should NOT return occupied position
        availablePositions.Should().NotContain("1iC"); // Other member's position
    }

    [Fact]
    public void GetRoles_Should_Return_All_Positions_When_Unit_Has_No_Chain_Of_Command()
    {
        // Arrange
        var unit = new DomainUnit
        {
            Id = _unitId,
            Name = "Test Unit",
            ChainOfCommand = null // No chain of command set
        };

        _mockUnitsContext.Setup(x => x.GetSingle(_unitId)).Returns(unit);

        // Act
        var result = _controller.GetRoles(_memberId, _unitId) as RolesDataset;

        // Assert
        result.Should().NotBeNull();
        result.UnitRoles.Should().NotBeNull();

        // Should return all positions since none are occupied
        var availablePositions = result.UnitRoles.Select(x => x.Name).ToList();
        availablePositions.Should().Contain("1iC");
        availablePositions.Should().Contain("2iC");
        availablePositions.Should().Contain("3iC");
        availablePositions.Should().Contain("NCOiC");
    }

    [Fact]
    public void GetRoles_Should_Return_All_Positions_When_Unit_Has_Empty_Chain_Of_Command()
    {
        // Arrange
        var unit = new DomainUnit
        {
            Id = _unitId,
            Name = "Test Unit",
            ChainOfCommand = new ChainOfCommand() // Empty chain of command
        };

        _mockUnitsContext.Setup(x => x.GetSingle(_unitId)).Returns(unit);

        // Act
        var result = _controller.GetRoles(_memberId, _unitId) as RolesDataset;

        // Assert
        result.Should().NotBeNull();
        result.UnitRoles.Should().NotBeNull();

        // Should return all positions since none are occupied
        var availablePositions = result.UnitRoles.Select(x => x.Name).ToList();
        availablePositions.Should().Contain("1iC");
        availablePositions.Should().Contain("2iC");
        availablePositions.Should().Contain("3iC");
        availablePositions.Should().Contain("NCOiC");
    }

    [Fact]
    public void GetRoles_Should_Return_Empty_Positions_When_Requesting_Available_Roles()
    {
        // Arrange
        var memberId = "member123";
        var unitId = "unit456";

        var unit = new DomainUnit
        {
            Id = unitId,
            Name = "Test Unit",
            ChainOfCommand = new ChainOfCommand
            {
                First = memberId, // Member is 1iC
                Second = "other_member", // 2iC is taken by someone else
                // ThreeIC and NCOIC are empty - should be available
            }
        };

        _mockUnitsContext.Setup(x => x.GetSingle(unitId)).Returns(unit);

        // Act
        var result = _controller.GetRoles(memberId, unitId);

        // Assert
        result.Should().NotBeNull();
        result.UnitRoles.Should().NotBeNull();

        var availablePositions = result.UnitRoles.Select(r => r.Name).ToList();
        availablePositions.Should().NotContain("1iC", "because the member already has this position");
        availablePositions.Should().NotContain("2iC", "because someone else has this position");
        availablePositions.Should().Contain("3iC", "because this position is empty");
        availablePositions.Should().Contain("NCOiC", "because this position is empty");
    }
}
