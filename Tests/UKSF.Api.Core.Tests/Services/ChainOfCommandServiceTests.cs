using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services;

public class ChainOfCommandServiceTests
{
    private readonly Mock<IUnitsContext> _mockUnitsContext;
    private readonly Mock<IAccountService> _mockAccountService;
    private readonly ChainOfCommandService _chainOfCommandService;

    private readonly string _commanderId = ObjectId.GenerateNewId().ToString();
    private readonly string _recipientId = ObjectId.GenerateNewId().ToString();
    private readonly string _contextUserId = ObjectId.GenerateNewId().ToString();
    private readonly string _unitId = ObjectId.GenerateNewId().ToString();
    private readonly string _parentUnitId = ObjectId.GenerateNewId().ToString();
    private readonly string _rootUnitId = ObjectId.GenerateNewId().ToString();

    public ChainOfCommandServiceTests()
    {
        _mockUnitsContext = new Mock<IUnitsContext>();
        var mockHttpContextService = new Mock<IHttpContextService>();
        _mockAccountService = new Mock<IAccountService>();

        _chainOfCommandService = new ChainOfCommandService(_mockUnitsContext.Object, mockHttpContextService.Object, _mockAccountService.Object);

        // Setup common mocks
        mockHttpContextService.Setup(x => x.GetUserId()).Returns(_contextUserId);
    }

    [Theory]
    [InlineData(0, "1iC")]
    [InlineData(1, "2iC")]
    [InlineData(2, "3iC")]
    [InlineData(3, "NCOiC")]
    [InlineData(999, "")]
    public void GetChainOfCommandPositionName_Should_Return_Correct_Position_Name(int order, string expected)
    {
        // Act
        var result = _chainOfCommandService.GetChainOfCommandPositionName(order);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("1iC", 0)]
    [InlineData("2iC", 1)]
    [InlineData("3iC", 2)]
    [InlineData("NCOiC", 3)]
    [InlineData("NonExistent", 0)] // Default value for key not found
    public void GetChainOfCommandPositionOrder_Should_Return_Correct_Order(string positionName, int expected)
    {
        // Act
        var result = _chainOfCommandService.GetChainOfCommandPositionOrder(positionName);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetCommanderPositionName_Should_Return_1iC()
    {
        // Act
        var result = _chainOfCommandService.GetCommanderPositionName();

        // Assert
        result.Should().Be("1iC");
    }

    [Fact]
    public void ResolveChain_Full_Mode_Should_Return_All_Commanders_Up_Chain()
    {
        // Arrange
        var commanderId1 = ObjectId.GenerateNewId().ToString();
        var commanderId2 = ObjectId.GenerateNewId().ToString();
        var commanderId3 = ObjectId.GenerateNewId().ToString();

        var rootUnit = CreateUnit(_rootUnitId, "Root Unit", hasCommander: true, commanderId: commanderId3);
        var parentUnit = CreateUnit(_parentUnitId, "Parent Unit", hasCommander: true, parent: _rootUnitId, commanderId: commanderId2);
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: true, parent: _parentUnitId, commanderId: commanderId1);

        // Setup unit retrieval by ID for parent lookup
        _mockUnitsContext.Setup(x => x.GetSingle(_unitId)).Returns(unit);
        _mockUnitsContext.Setup(x => x.GetSingle(_parentUnitId)).Returns(parentUnit);
        _mockUnitsContext.Setup(x => x.GetSingle(_rootUnitId)).Returns(rootUnit);

        // Setup GetSingle with predicate to find units by ID for parent traversal
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns<Func<DomainUnit, bool>>(predicate => predicate(unit) ? unit :
                                                              predicate(parentUnit)    ? parentUnit :
                                                              predicate(rootUnit)      ? rootUnit : null
                         );

        // Act
        var result = _chainOfCommandService.ResolveChain(ChainOfCommandMode.Full, _recipientId, unit, null);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(commanderId1);
        result.Should().Contain(commanderId2);
        result.Should().Contain(commanderId3);
    }

    [Fact]
    public void ResolveChain_Next_Commander_Mode_Should_Return_Next_Commander()
    {
        // Arrange
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: true);

        // Act
        var result = _chainOfCommandService.ResolveChain(ChainOfCommandMode.Next_Commander, _recipientId, unit, null);

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(_commanderId);
    }

    [Fact]
    public void ResolveChain_Next_Commander_Exclude_Self_Should_Exclude_Context_User()
    {
        // Arrange
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: true, commanderId: _contextUserId);

        // Act
        var result = _chainOfCommandService.ResolveChain(ChainOfCommandMode.Next_Commander_Exclude_Self, _recipientId, unit, null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ResolveChain_Should_Exclude_Recipient_From_Chain()
    {
        // Arrange
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: true, commanderId: _recipientId);

        // Act
        var result = _chainOfCommandService.ResolveChain(ChainOfCommandMode.Next_Commander, _recipientId, unit, null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void InContextChainOfCommand_Should_Return_True_For_Same_User()
    {
        // Arrange
        var account = new DomainAccount { Id = _contextUserId, UnitAssignment = "Test Unit" };
        _mockAccountService.Setup(x => x.GetUserAccount()).Returns(account);

        // Act
        var result = _chainOfCommandService.InContextChainOfCommand(_contextUserId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void InContextChainOfCommand_Should_Return_False_For_Non_Member()
    {
        // Arrange
        var account = new DomainAccount { Id = _contextUserId, UnitAssignment = "Test Unit" };
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: false);

        _mockAccountService.Setup(x => x.GetUserAccount()).Returns(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(unit);

        // Act
        var result = _chainOfCommandService.InContextChainOfCommand(_recipientId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MemberHasChainOfCommandPositionInCombatOrAuxiliaryUnits_Should_Return_True_For_Combat_Unit()
    {
        // Arrange
        var combatUnit = CreateUnit(_unitId, "Combat Unit", hasCommander: true, commanderId: _recipientId);
        combatUnit.Branch = UnitBranch.Combat;
        combatUnit.ChainOfCommand.First = _recipientId;

        var units = new List<DomainUnit> { combatUnit };
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(predicate => units.Where(predicate));

        // Act
        var result = _chainOfCommandService.MemberHasChainOfCommandPositionInCombatOrAuxiliaryUnits(_recipientId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MemberHasChainOfCommandPositionInCombatOrAuxiliaryUnits_Should_Return_True_For_Auxiliary_Unit()
    {
        // Arrange
        var auxiliaryUnit = CreateUnit(_unitId, "Auxiliary Unit", hasCommander: true, commanderId: _recipientId);
        auxiliaryUnit.Branch = UnitBranch.Auxiliary;
        auxiliaryUnit.ChainOfCommand.First = _recipientId;

        var units = new List<DomainUnit> { auxiliaryUnit };
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(predicate => units.Where(predicate));

        // Act
        var result = _chainOfCommandService.MemberHasChainOfCommandPositionInCombatOrAuxiliaryUnits(_recipientId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MemberHasChainOfCommandPositionInCombatOrAuxiliaryUnits_Should_Return_False_For_Secondary_Unit()
    {
        // Arrange
        var secondaryUnit = CreateUnit(_unitId, "Secondary Unit", hasCommander: true, commanderId: _recipientId);
        secondaryUnit.Branch = UnitBranch.Secondary;
        secondaryUnit.ChainOfCommand.First = _recipientId;

        var units = new List<DomainUnit> { secondaryUnit };
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(predicate => units.Where(predicate));

        // Act
        var result = _chainOfCommandService.MemberHasChainOfCommandPositionInCombatOrAuxiliaryUnits(_recipientId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MemberHasChainOfCommandPositionInCombatOrAuxiliaryUnits_Should_Return_True_When_Member_In_Both_Combat_And_Secondary()
    {
        // Arrange
        var combatUnit = CreateUnit("combat-unit-id", "Combat Unit", hasCommander: true, commanderId: _recipientId);
        combatUnit.Branch = UnitBranch.Combat;
        combatUnit.ChainOfCommand.First = _recipientId;

        var secondaryUnit = CreateUnit("secondary-unit-id", "Secondary Unit", hasCommander: true, commanderId: _recipientId);
        secondaryUnit.Branch = UnitBranch.Secondary;
        secondaryUnit.ChainOfCommand.First = _recipientId;

        var units = new List<DomainUnit> { combatUnit, secondaryUnit };
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(predicate => units.Where(predicate));

        // Act
        var result = _chainOfCommandService.MemberHasChainOfCommandPositionInCombatOrAuxiliaryUnits(_recipientId);

        // Assert
        result.Should().BeTrue(); // Should return true because member has position in combat unit
    }

    [Fact]
    public void MemberHasChainOfCommandPositionInCombatOrAuxiliaryUnits_Should_Return_False_When_No_Units()
    {
        // Arrange
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns<Func<DomainUnit, bool>>(predicate => new List<DomainUnit>().Where(predicate));

        // Act
        var result = _chainOfCommandService.MemberHasChainOfCommandPositionInCombatOrAuxiliaryUnits(_recipientId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MemberHasChainOfCommandPositionInCombatOrAuxiliaryUnits_Should_Return_False_When_Member_Has_No_Chain_Of_Command_Position()
    {
        // Arrange
        var combatUnit = CreateUnit(_unitId, "Combat Unit", hasCommander: false);
        combatUnit.Branch = UnitBranch.Combat;
        // No chain of command position for the member

        var units = new List<DomainUnit> { combatUnit };
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(predicate => units.Where(predicate));

        // Act
        var result = _chainOfCommandService.MemberHasChainOfCommandPositionInCombatOrAuxiliaryUnits(_recipientId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetMemberChainOfCommandPosition_Remove_ShouldNotMutateCachedUnit()
    {
        var memberId = ObjectId.GenerateNewId().ToString();
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: true, commanderId: memberId);
        unit.ChainOfCommand.Second = ObjectId.GenerateNewId().ToString();

        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(unit);

        await _chainOfCommandService.SetMemberChainOfCommandPosition(memberId, unit);

        unit.ChainOfCommand.First.Should().Be(memberId, "the cached unit's ChainOfCommand should not be mutated");
    }

    [Fact]
    public async Task SetMemberChainOfCommandPosition_SetNew_ShouldNotMutateCachedUnit()
    {
        var memberId = ObjectId.GenerateNewId().ToString();
        var existingCommander = ObjectId.GenerateNewId().ToString();
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: true, commanderId: existingCommander);

        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(unit);

        await _chainOfCommandService.SetMemberChainOfCommandPosition(memberId, unit, "2iC");

        unit.ChainOfCommand.Second.Should().BeNull("the cached unit's ChainOfCommand should not be mutated");
    }

    [Fact]
    public void HasChainOfCommandPosition_By_UnitId_Should_Return_True_When_Position_Is_Filled()
    {
        // Arrange
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: true);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(unit);

        // Act
        var result = _chainOfCommandService.HasChainOfCommandPosition(_unitId, "1iC");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasChainOfCommandPosition_By_UnitId_Should_Return_False_When_Position_Is_Empty()
    {
        // Arrange
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: false);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(unit);

        // Act
        var result = _chainOfCommandService.HasChainOfCommandPosition(_unitId, "1iC");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetMemberChainOfCommandOrder_Should_Return_Correct_Order_For_1iC()
    {
        // Arrange
        var account = new DomainAccount { Id = _commanderId };
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: true, commanderId: _commanderId);

        // Act
        var result = _chainOfCommandService.GetMemberChainOfCommandOrder(account, unit);

        // Assert
        result.Should().Be(int.MaxValue); // int.MaxValue - 0 (order of 1iC is 0)
    }

    [Fact]
    public void GetMemberChainOfCommandOrder_Should_Return_Negative_One_When_Member_Has_No_Position()
    {
        // Arrange
        var nonMemberId = ObjectId.GenerateNewId().ToString();
        var account = new DomainAccount { Id = nonMemberId };
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: true, commanderId: _commanderId);

        // Act
        var result = _chainOfCommandService.GetMemberChainOfCommandOrder(account, unit);

        // Assert
        result.Should().Be(-1);
    }

    [Fact]
    public void GetChainOfCommandPosition_Should_Return_Correct_Position_Name()
    {
        // Arrange
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: true, commanderId: _commanderId);

        // Act
        var result = _chainOfCommandService.GetChainOfCommandPosition(unit, _commanderId);

        // Assert
        result.Should().Be("1iC");
    }

    [Fact]
    public void GetChainOfCommandPosition_Should_Return_Empty_When_Member_Has_No_Position()
    {
        // Arrange
        var nonMemberId = ObjectId.GenerateNewId().ToString();
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: true, commanderId: _commanderId);

        // Act
        var result = _chainOfCommandService.GetChainOfCommandPosition(unit, nonMemberId);

        // Assert
        result.Should().BeEmpty();
    }

    private DomainUnit CreateUnit(string id, string name, bool hasCommander = false, string commanderId = null, string parent = "", string shortname = null)
    {
        var unit = new DomainUnit
        {
            Id = id,
            Name = name,
            Shortname = shortname ?? name,
            Parent = parent,
            Members = new List<string>(),
            ChainOfCommand = new ChainOfCommand(),
            Branch = UnitBranch.Combat // Default to Combat for existing tests
        };

        if (hasCommander)
        {
            var actualCommanderId = commanderId ?? _commanderId;
            unit.ChainOfCommand.First = actualCommanderId;
        }

        return unit;
    }
}
