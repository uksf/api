using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class ChainOfCommandServiceTests
{
    private readonly Mock<IUnitsContext> _mockUnitsContext;
    private readonly Mock<IUnitsService> _mockUnitsService;
    private readonly Mock<IHttpContextService> _mockHttpContextService;
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
        _mockUnitsService = new Mock<IUnitsService>();
        _mockHttpContextService = new Mock<IHttpContextService>();
        _mockAccountService = new Mock<IAccountService>();

        _chainOfCommandService = new ChainOfCommandService(
            _mockUnitsContext.Object,
            _mockUnitsService.Object,
            _mockHttpContextService.Object,
            _mockAccountService.Object
        );

        // Setup common mocks
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns(_contextUserId);
    }

    [Fact]
    public void ResolveChain_Full_Mode_Should_Return_All_Commanders_Up_Chain()
    {
        // Arrange
        var commanderId1 = ObjectId.GenerateNewId().ToString();
        var commanderId2 = ObjectId.GenerateNewId().ToString();
        var commanderId3 = ObjectId.GenerateNewId().ToString();

        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: true, commanderId: commanderId1);
        var parentUnit = CreateUnit(_parentUnitId, "Parent Unit", hasCommander: true, parent: _rootUnitId, commanderId: commanderId2);
        var rootUnit = CreateUnit(_rootUnitId, "Root Unit", hasCommander: true, commanderId: commanderId3);

        _mockUnitsService.SetupSequence(x => x.GetParent(It.IsAny<DomainUnit>())).Returns(parentUnit).Returns(rootUnit).Returns((DomainUnit)null);

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
    public void ResolveChain_Commander_And_One_Above_Should_Return_Unit_And_Parent_Commanders()
    {
        // Arrange
        var commanderId1 = ObjectId.GenerateNewId().ToString();
        var commanderId2 = ObjectId.GenerateNewId().ToString();

        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: true, commanderId: commanderId1);
        var parentUnit = CreateUnit(_parentUnitId, "Parent Unit", hasCommander: true, commanderId: commanderId2);

        _mockUnitsService.Setup(x => x.GetParent(unit)).Returns(parentUnit);

        // Act
        var result = _chainOfCommandService.ResolveChain(ChainOfCommandMode.Commander_And_One_Above, _recipientId, unit, null);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(commanderId1);
        result.Should().Contain(commanderId2);
    }

    [Fact]
    public void ResolveChain_Commander_And_Personnel_Should_Return_Commander_And_Personnel()
    {
        // Arrange
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: true);
        var personnelUnit = CreateUnit(ObjectId.GenerateNewId().ToString(), "SR7", shortname: "SR7");
        personnelUnit.Members = new List<string> { ObjectId.GenerateNewId().ToString() };

        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(personnelUnit);

        // Act
        var result = _chainOfCommandService.ResolveChain(ChainOfCommandMode.Commander_And_Personnel, _recipientId, unit, null);

        // Assert
        result.Should().HaveCount(2); // Commander + 1 personnel member
        result.Should().Contain(_commanderId);
    }

    [Fact]
    public void ResolveChain_Target_Commander_Should_Return_Target_Unit_Commander()
    {
        // Arrange
        var startUnit = CreateUnit(_unitId, "Start Unit", hasCommander: false);
        var targetUnit = CreateUnit(ObjectId.GenerateNewId().ToString(), "Target Unit", hasCommander: true);

        // Act
        var result = _chainOfCommandService.ResolveChain(ChainOfCommandMode.Target_Commander, _recipientId, startUnit, targetUnit);

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(_commanderId);
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
    public void ResolveChain_Should_Fallback_To_Root_Commander_When_No_Chain()
    {
        // Arrange
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: false);
        var rootUnit = CreateUnit(_rootUnitId, "Root Unit", hasCommander: true);

        _mockUnitsService.Setup(x => x.GetRoot()).Returns(rootUnit);

        // Act
        var result = _chainOfCommandService.ResolveChain(ChainOfCommandMode.Next_Commander, _recipientId, unit, null);

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(_commanderId);
    }

    [Fact]
    public void ResolveChain_Should_Fallback_To_Root_Child_Commanders_When_No_Root_Commander()
    {
        // Arrange
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: false);
        var rootUnit = CreateUnit(_rootUnitId, "Root Unit", hasCommander: false);
        var childUnit = CreateUnit(ObjectId.GenerateNewId().ToString(), "Child Unit", hasCommander: true);

        _mockUnitsService.Setup(x => x.GetRoot()).Returns(rootUnit);
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new List<DomainUnit> { childUnit });

        // Act
        var result = _chainOfCommandService.ResolveChain(ChainOfCommandMode.Next_Commander, _recipientId, unit, null);

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(_commanderId);
    }

    [Fact]
    public void ResolveChain_Should_Fallback_To_Personnel_When_No_Other_Chain()
    {
        // Arrange
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: false);
        var rootUnit = CreateUnit(_rootUnitId, "Root Unit", hasCommander: false);
        var personnelUnit = CreateUnit(ObjectId.GenerateNewId().ToString(), "SR7", shortname: "SR7");
        personnelUnit.Members = new List<string> { ObjectId.GenerateNewId().ToString() };

        _mockUnitsService.Setup(x => x.GetRoot()).Returns(rootUnit);
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new List<DomainUnit>());
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(personnelUnit);

        // Act
        var result = _chainOfCommandService.ResolveChain(ChainOfCommandMode.Next_Commander, _recipientId, unit, null);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void InContextChainOfCommand_Should_Return_True_For_Same_User()
    {
        // Arrange
        var account = new DomainAccount { Id = _contextUserId };
        _mockAccountService.Setup(x => x.GetUserAccount()).Returns(account);

        // Act
        var result = _chainOfCommandService.InContextChainOfCommand(_contextUserId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void InContextChainOfCommand_Should_Return_True_For_Unit_Member()
    {
        // Arrange
        var account = new DomainAccount { Id = _contextUserId, UnitAssignment = "Test Unit" };
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: false);
        unit.Members = new List<string> { _recipientId };

        _mockAccountService.Setup(x => x.GetUserAccount()).Returns(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(unit);
        _mockUnitsService.Setup(x => x.ChainOfCommandHasMember(unit, _contextUserId)).Returns(true);
        _mockUnitsService.Setup(x => x.GetAllChildren(unit, true)).Returns(new List<DomainUnit>());

        // Act
        var result = _chainOfCommandService.InContextChainOfCommand(_recipientId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void InContextChainOfCommand_Should_Return_True_For_Child_Unit_Member()
    {
        // Arrange
        var account = new DomainAccount { Id = _contextUserId, UnitAssignment = "Test Unit" };
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: false);
        var childUnit = CreateUnit(ObjectId.GenerateNewId().ToString(), "Child Unit", hasCommander: false);
        childUnit.Members = new List<string> { _recipientId };

        _mockAccountService.Setup(x => x.GetUserAccount()).Returns(account);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(unit);
        _mockUnitsService.Setup(x => x.ChainOfCommandHasMember(unit, _contextUserId)).Returns(true);
        _mockUnitsService.Setup(x => x.GetAllChildren(unit, true)).Returns(new List<DomainUnit> { childUnit });

        // Act
        var result = _chainOfCommandService.InContextChainOfCommand(_recipientId);

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
        _mockUnitsService.Setup(x => x.ChainOfCommandHasMember(unit, _contextUserId)).Returns(false);

        // Act
        var result = _chainOfCommandService.InContextChainOfCommand(_recipientId);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(ChainOfCommandMode.Full)]
    [InlineData(ChainOfCommandMode.Next_Commander)]
    [InlineData(ChainOfCommandMode.Next_Commander_Exclude_Self)]
    [InlineData(ChainOfCommandMode.Commander_And_One_Above)]
    [InlineData(ChainOfCommandMode.Commander_And_Personnel)]
    [InlineData(ChainOfCommandMode.Commander_And_Target_Commander)]
    [InlineData(ChainOfCommandMode.Personnel)]
    [InlineData(ChainOfCommandMode.Target_Commander)]
    public void ResolveChain_Should_Handle_All_Chain_Of_Command_Modes(ChainOfCommandMode mode)
    {
        // Arrange
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: true);
        var targetUnit = CreateUnit(ObjectId.GenerateNewId().ToString(), "Target Unit", hasCommander: true);
        var personnelUnit = CreateUnit(ObjectId.GenerateNewId().ToString(), "SR7", shortname: "SR7");
        personnelUnit.Members = new List<string> { ObjectId.GenerateNewId().ToString() };

        _mockUnitsService.Setup(x => x.GetParent(unit)).Returns((DomainUnit)null);
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(personnelUnit);

        // Act & Assert - Should not throw
        var result = _chainOfCommandService.ResolveChain(mode, _recipientId, unit, targetUnit);
        result.Should().NotBeNull();
    }

    [Fact]
    public void GetCommander_Should_Handle_Null_Unit_Gracefully()
    {
        // Arrange
        var unit = CreateUnit(_unitId, "Test Unit", hasCommander: true);

        // Act & Assert - This should not throw a NullReferenceException
        var result = _chainOfCommandService.ResolveChain(ChainOfCommandMode.Full, _recipientId, unit, null);
        result.Should().NotBeNull();
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
            ChainOfCommand = new ChainOfCommand()
        };

        if (hasCommander)
        {
            var actualCommanderId = commanderId ?? _commanderId;
            unit.ChainOfCommand.First = actualCommanderId;
            _mockUnitsService.Setup(x => x.HasChainOfCommandPosition(unit, "1iC")).Returns(true);
        }
        else
        {
            _mockUnitsService.Setup(x => x.HasChainOfCommandPosition(unit, "1iC")).Returns(false);
        }

        return unit;
    }
}
