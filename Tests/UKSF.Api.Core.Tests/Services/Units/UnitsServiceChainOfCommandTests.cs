using System;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Mappers;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services.Units;

public class UnitsServiceChainOfCommandTests
{
    private readonly Mock<IUnitsContext> _mockUnitsContext;
    private readonly Mock<IRolesService> _mockRolesService;
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly UnitsService _unitsService;
    private readonly string _unitId = "unit123";
    private readonly string _memberId = "member123";

    public UnitsServiceChainOfCommandTests()
    {
        _mockUnitsContext = new Mock<IUnitsContext>();
        var mockRolesContext = new Mock<IRolesContext>();
        var mockRanksService = new Mock<IRanksService>();
        _mockRolesService = new Mock<IRolesService>();
        var mockDisplayNameService = new Mock<IDisplayNameService>();
        _mockAccountContext = new Mock<IAccountContext>();
        var mockUnitMapper = new Mock<IUnitMapper>();

        _unitsService = new UnitsService(
            _mockUnitsContext.Object,
            mockRolesContext.Object,
            mockRanksService.Object,
            _mockRolesService.Object,
            mockDisplayNameService.Object,
            _mockAccountContext.Object,
            mockUnitMapper.Object
        );
    }

    [Fact]
    public async Task SetMemberChainOfCommandPosition_Should_Set_Position_For_Member()
    {
        // Arrange
        var unit = new DomainUnit { Id = _unitId, ChainOfCommand = new ChainOfCommand() };
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(unit);

        // Act
        await _unitsService.SetMemberChainOfCommandPosition(_memberId, _unitId, "1iC");

        // Assert
        _mockUnitsContext.Verify(x => x.Update(_unitId, It.IsAny<UpdateDefinition<DomainUnit>>()), Times.Once);
    }

    [Fact]
    public async Task SetMemberChainOfCommandPosition_Should_Remove_Position_When_Position_Is_Empty()
    {
        // Arrange
        var chainOfCommand = new ChainOfCommand { First = _memberId };
        var unit = new DomainUnit { Id = _unitId, ChainOfCommand = chainOfCommand };
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(unit);

        // Act
        await _unitsService.SetMemberChainOfCommandPosition(_memberId, _unitId, "");

        // Assert
        _mockUnitsContext.Verify(x => x.Update(_unitId, It.IsAny<UpdateDefinition<DomainUnit>>()), Times.Once);
    }

    [Fact]
    public void HasChainOfCommandPosition_Should_Return_True_When_Unit_Has_Position()
    {
        // Arrange
        var chainOfCommand = new ChainOfCommand { First = _memberId };
        var unit = new DomainUnit { Id = _unitId, ChainOfCommand = chainOfCommand };
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(unit);

        // Act
        var result = _unitsService.HasChainOfCommandPosition(_unitId, "1iC");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasChainOfCommandPosition_Should_Return_False_When_Unit_Does_Not_Have_Position()
    {
        // Arrange
        var unit = new DomainUnit { Id = _unitId, ChainOfCommand = new ChainOfCommand() };
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(unit);

        // Act
        var result = _unitsService.HasChainOfCommandPosition(_unitId, "1iC");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ChainOfCommandHasMember_Should_Return_True_When_Member_Has_Position()
    {
        // Arrange
        var chainOfCommand = new ChainOfCommand { First = _memberId };
        var unit = new DomainUnit { Id = _unitId, ChainOfCommand = chainOfCommand };

        // Act
        var result = _unitsService.ChainOfCommandHasMember(unit, _memberId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ChainOfCommandHasMember_Should_Return_False_When_Member_Has_No_Position()
    {
        // Arrange
        var unit = new DomainUnit { Id = _unitId, ChainOfCommand = new ChainOfCommand() };

        // Act
        var result = _unitsService.ChainOfCommandHasMember(unit, _memberId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MemberHasChainOfCommandPosition_Should_Return_True_When_Member_Has_Specific_Position()
    {
        // Arrange
        var chainOfCommand = new ChainOfCommand { First = _memberId };
        var unit = new DomainUnit { Id = _unitId, ChainOfCommand = chainOfCommand };

        // Act
        var result = _unitsService.MemberHasChainOfCommandPosition(_memberId, unit, "1iC");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MemberHasChainOfCommandPosition_Should_Return_False_When_Member_Does_Not_Have_Specific_Position()
    {
        // Arrange
        var chainOfCommand = new ChainOfCommand { First = "other-member" };
        var unit = new DomainUnit { Id = _unitId, ChainOfCommand = chainOfCommand };

        // Act
        var result = _unitsService.MemberHasChainOfCommandPosition(_memberId, unit, "1iC");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetMemberChainOfCommandOrder_Should_Return_Correct_Order()
    {
        // Arrange
        var chainOfCommand = new ChainOfCommand { Second = _memberId }; // Should be order 1
        var unit = new DomainUnit { Id = _unitId, ChainOfCommand = chainOfCommand };
        var account = new DomainAccount { Id = _memberId };

        _mockRolesService.Setup(x => x.GetUnitRoleOrderByName("2iC")).Returns(1);

        // Act
        var result = _unitsService.GetMemberChainOfCommandOrder(account, unit);

        // Assert
        result.Should().Be(int.MaxValue - 1); // Inverted for sorting
    }

    [Fact]
    public void GetMemberChainOfCommandOrder_Should_Return_Minus_One_When_Member_Has_No_Position()
    {
        // Arrange
        var unit = new DomainUnit { Id = _unitId, ChainOfCommand = new ChainOfCommand() };
        var account = new DomainAccount { Id = _memberId };

        // Act
        var result = _unitsService.GetMemberChainOfCommandOrder(account, unit);

        // Assert
        result.Should().Be(-1);
    }
}
