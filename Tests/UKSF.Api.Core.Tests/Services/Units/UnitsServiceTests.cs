using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Mappers;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services.Units;

public class UnitsServiceTests
{
    private readonly Mock<IUnitsContext> _mockUnitsContext;
    private readonly Mock<IRolesContext> _mockRolesContext;
    private readonly Mock<IRanksService> _mockRanksService;
    private readonly Mock<IRolesService> _mockRolesService;
    private readonly Mock<IDisplayNameService> _mockDisplayNameService;
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly Mock<IUnitMapper> _mockUnitMapper;
    private readonly UnitsService _unitsService;

    private readonly string _combatRootId = ObjectId.GenerateNewId().ToString();
    private readonly string _auxiliaryRootId = ObjectId.GenerateNewId().ToString();
    private readonly string _unitId = ObjectId.GenerateNewId().ToString();
    private readonly string _memberId = ObjectId.GenerateNewId().ToString();

    public UnitsServiceTests()
    {
        _mockUnitsContext = new Mock<IUnitsContext>();
        _mockRolesContext = new Mock<IRolesContext>();
        _mockRanksService = new Mock<IRanksService>();
        _mockRolesService = new Mock<IRolesService>();
        _mockDisplayNameService = new Mock<IDisplayNameService>();
        _mockAccountContext = new Mock<IAccountContext>();
        _mockUnitMapper = new Mock<IUnitMapper>();

        _unitsService = new UnitsService(
            _mockUnitsContext.Object,
            _mockRanksService.Object,
            _mockRolesService.Object,
            _mockDisplayNameService.Object,
            _mockAccountContext.Object,
            _mockUnitMapper.Object
        );
    }

    [Fact]
    public void GetSortedUnits_Should_Return_Combat_Then_Auxiliary_Units_In_Order()
    {
        // Arrange
        var combatRoot = new DomainUnit
        {
            Id = _combatRootId,
            Name = "UKSF",
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString(),
            Order = 0
        };
        var combatChild1 = new DomainUnit
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "SFSG",
            Branch = UnitBranch.Combat,
            Parent = _combatRootId,
            Order = 1
        };
        var combatChild2 = new DomainUnit
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "JSFAW",
            Branch = UnitBranch.Combat,
            Parent = _combatRootId,
            Order = 0
        };

        var auxiliaryRoot = new DomainUnit
        {
            Id = _auxiliaryRootId,
            Name = "ELCOM",
            Branch = UnitBranch.Auxiliary,
            Parent = ObjectId.Empty.ToString(),
            Order = 0
        };
        var auxiliaryChild = new DomainUnit
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "SR1",
            Branch = UnitBranch.Auxiliary,
            Parent = _auxiliaryRootId,
            Order = 0
        };

        var secondaryRoot = new DomainUnit
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "SECONDARY",
            Branch = UnitBranch.Secondary,
            Parent = ObjectId.Empty.ToString(),
            Order = 0
        };

        var allUnits = new List<DomainUnit>
        {
            combatRoot,
            combatChild1,
            combatChild2,
            auxiliaryRoot,
            auxiliaryChild,
            secondaryRoot
        };

        _mockUnitsContext.Setup(x => x.GetSingle(It.Is<Func<DomainUnit, bool>>(f => f(combatRoot) && !f(auxiliaryRoot) && !f(secondaryRoot))))
                         .Returns(combatRoot);
        _mockUnitsContext.Setup(x => x.GetSingle(It.Is<Func<DomainUnit, bool>>(f => f(auxiliaryRoot) && !f(combatRoot) && !f(secondaryRoot))))
                         .Returns(auxiliaryRoot);
        _mockUnitsContext.Setup(x => x.GetSingle(It.Is<Func<DomainUnit, bool>>(f => f(secondaryRoot) && !f(combatRoot) && !f(auxiliaryRoot))))
                         .Returns(secondaryRoot);
        _mockUnitsContext.Setup(x => x.Get(It.Is<Func<DomainUnit, bool>>(f => f(combatChild1) && f(combatChild2) && !f(auxiliaryChild))))
                         .Returns(new List<DomainUnit> { combatChild2, combatChild1 });
        _mockUnitsContext.Setup(x => x.Get(It.Is<Func<DomainUnit, bool>>(f => f(auxiliaryChild) && !f(combatChild1))))
                         .Returns(new List<DomainUnit> { auxiliaryChild });
        _mockUnitsContext.Setup(x => x.Get(It.Is<Func<DomainUnit, bool>>(f => f(secondaryRoot)))).Returns(new List<DomainUnit>());

        // Act
        var result = _unitsService.GetSortedUnits().ToList();

        // Assert
        result.Should().HaveCount(6);
        result[0].Should().Be(combatRoot);
        result[1].Should().Be(combatChild2); // Order 0 comes first
        result[2].Should().Be(combatChild1); // Order 1 comes second
        result[3].Should().Be(auxiliaryRoot);
        result[4].Should().Be(auxiliaryChild);
        result[5].Should().Be(secondaryRoot);
    }

    [Fact]
    public void GetSortedUnits_With_Predicate_Should_Filter_Results()
    {
        // Arrange
        var combatRoot = new DomainUnit
        {
            Id = _combatRootId,
            Name = "UKSF",
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString()
        };
        var auxiliaryRoot = new DomainUnit
        {
            Id = _auxiliaryRootId,
            Name = "ELCOM",
            Branch = UnitBranch.Auxiliary,
            Parent = ObjectId.Empty.ToString()
        };
        var secondaryRoot = new DomainUnit
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "SECONDARY",
            Branch = UnitBranch.Secondary,
            Parent = ObjectId.Empty.ToString()
        };

        _mockUnitsContext.Setup(x => x.GetSingle(It.Is<Func<DomainUnit, bool>>(f => f(combatRoot) && !f(auxiliaryRoot) && !f(secondaryRoot))))
                         .Returns(combatRoot);
        _mockUnitsContext.Setup(x => x.GetSingle(It.Is<Func<DomainUnit, bool>>(f => f(auxiliaryRoot) && !f(combatRoot) && !f(secondaryRoot))))
                         .Returns(auxiliaryRoot);
        _mockUnitsContext.Setup(x => x.GetSingle(It.Is<Func<DomainUnit, bool>>(f => f(secondaryRoot) && !f(combatRoot) && !f(auxiliaryRoot))))
                         .Returns(secondaryRoot);
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new List<DomainUnit>());

        // Act
        var result = _unitsService.GetSortedUnits(x => x.Branch == UnitBranch.Combat).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Should().Be(combatRoot);
    }

    [Fact]
    public async Task AddMember_Should_Add_Member_To_Unit()
    {
        // Arrange
        var unit = new DomainUnit { Id = _unitId, Members = new List<string>() };
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns((DomainUnit)null);

        // Act
        await _unitsService.AddMember(_memberId, _unitId);

        // Assert
        _mockUnitsContext.Verify(x => x.Update(_unitId, It.IsAny<UpdateDefinition<DomainUnit>>()), Times.Once);
    }

    [Fact]
    public async Task AddMember_Should_Not_Add_If_Already_Member()
    {
        // Arrange
        var unit = new DomainUnit { Id = _unitId, Members = new List<string> { _memberId } };
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(unit);

        // Act
        await _unitsService.AddMember(_memberId, _unitId);

        // Assert
        _mockUnitsContext.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainUnit>>()), Times.Never);
    }

    [Fact]
    public async Task RemoveMember_By_UnitName_Should_Remove_Member()
    {
        // Arrange
        var unit = new DomainUnit
        {
            Id = _unitId,
            Name = "Test Unit",
            Members = new List<string> { _memberId }
        };
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(unit);

        // Act
        await _unitsService.RemoveMember(_memberId, "Test Unit");

        // Assert
        _mockUnitsContext.Verify(x => x.Update(_unitId, It.IsAny<UpdateDefinition<DomainUnit>>()), Times.Once);
    }

    [Fact]
    public async Task RemoveMember_By_UnitName_Should_Not_Remove_If_Unit_Not_Found()
    {
        // Arrange
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(null);

        // Act
        await _unitsService.RemoveMember(_memberId, "NonExistentUnit");

        // Assert
        _mockUnitsContext.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainUnit>>()), Times.Never);
    }

    [Fact]
    public void HasMember_Should_Return_True_When_Unit_Has_Member()
    {
        // Arrange
        var unit = new DomainUnit { Id = _unitId, Members = new List<string> { _memberId } };
        _mockUnitsContext.Setup(x => x.GetSingle(_unitId)).Returns(unit);

        // Act
        var result = _unitsService.HasMember(_unitId, _memberId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasMember_Should_Return_False_When_Unit_Does_Not_Have_Member()
    {
        // Arrange
        var unit = new DomainUnit { Id = _unitId, Members = new List<string>() };
        _mockUnitsContext.Setup(x => x.GetSingle(_unitId)).Returns(unit);

        // Act
        var result = _unitsService.HasMember(_unitId, _memberId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetRoot_Should_Return_Combat_Root_Unit()
    {
        // Arrange
        var combatRoot = new DomainUnit
        {
            Id = _combatRootId,
            Branch = UnitBranch.Combat,
            Parent = ObjectId.Empty.ToString()
        };
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(combatRoot);

        // Act
        var result = _unitsService.GetRoot();

        // Assert
        result.Should().Be(combatRoot);
        result.Branch.Should().Be(UnitBranch.Combat);
    }

    [Fact]
    public void GetAuxiliaryRoot_Should_Return_Auxiliary_Root_Unit()
    {
        // Arrange
        var auxiliaryRoot = new DomainUnit
        {
            Id = _auxiliaryRootId,
            Branch = UnitBranch.Auxiliary,
            Parent = ObjectId.Empty.ToString()
        };
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(auxiliaryRoot);

        // Act
        var result = _unitsService.GetAuxiliaryRoot();

        // Assert
        result.Should().Be(auxiliaryRoot);
        result.Branch.Should().Be(UnitBranch.Auxiliary);
    }

    [Fact]
    public void GetParent_Should_Return_Parent_Unit()
    {
        // Arrange
        var parentId = ObjectId.GenerateNewId().ToString();
        var parent = new DomainUnit { Id = parentId, Name = "Parent Unit" };
        var child = new DomainUnit
        {
            Id = _unitId,
            Name = "Child Unit",
            Parent = parentId
        };

        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(parent);

        // Act
        var result = _unitsService.GetParent(child);

        // Assert
        result.Should().Be(parent);
    }

    [Fact]
    public void GetParent_Should_Return_Null_When_No_Parent()
    {
        // Arrange
        var unit = new DomainUnit
        {
            Id = _unitId,
            Name = "Root Unit",
            Parent = string.Empty
        };

        // Act
        var result = _unitsService.GetParent(unit);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetChildren_Should_Return_Child_Units()
    {
        // Arrange
        var parent = new DomainUnit { Id = _unitId, Name = "Parent Unit" };
        var child1 = new DomainUnit
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Child 1",
            Parent = _unitId
        };
        var child2 = new DomainUnit
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Child 2",
            Parent = _unitId
        };

        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new List<DomainUnit> { child1, child2 });

        // Act
        var result = _unitsService.GetChildren(parent).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(child1);
        result.Should().Contain(child2);
    }

    [Fact]
    public void GetAllChildren_Should_Return_All_Descendant_Units()
    {
        // Arrange
        var parent = new DomainUnit { Id = _unitId, Name = "Parent Unit" };
        var child1Id = ObjectId.GenerateNewId().ToString();
        var child1 = new DomainUnit
        {
            Id = child1Id,
            Name = "Child 1",
            Parent = _unitId
        };
        var grandchild = new DomainUnit
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Grandchild",
            Parent = child1Id
        };

        _mockUnitsContext.SetupSequence(x => x.Get(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns(new List<DomainUnit> { child1 })
                         .Returns(new List<DomainUnit> { grandchild })
                         .Returns(new List<DomainUnit>());

        // Act
        var result = _unitsService.GetAllChildren(parent).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(child1);
        result.Should().Contain(grandchild);
    }

    [Fact]
    public void GetAllChildren_With_IncludeParent_Should_Include_Parent_Unit()
    {
        // Arrange
        var parent = new DomainUnit { Id = _unitId, Name = "Parent Unit" };
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new List<DomainUnit>());

        // Act
        var result = _unitsService.GetAllChildren(parent, true).ToList();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(parent);
    }

    [Fact]
    public void GetChainString_Should_Return_Unit_Chain_String()
    {
        // Arrange
        var grandparentId = ObjectId.GenerateNewId().ToString();
        var parentId = ObjectId.GenerateNewId().ToString();
        var grandparent = new DomainUnit
        {
            Id = grandparentId,
            Name = "UKSF",
            Parent = ObjectId.Empty.ToString()
        };
        var parent = new DomainUnit
        {
            Id = parentId,
            Name = "SFSG",
            Parent = grandparentId
        };
        var unit = new DomainUnit
        {
            Id = _unitId,
            Name = "1 Section",
            Parent = parentId
        };

        // Mock the GetSingle calls for the GetParents method
        _mockUnitsContext.SetupSequence(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns(parent) // First call for parent
                         .Returns(grandparent) // Second call for grandparent
                         .Returns((DomainUnit)null); // Third call returns null (no more parents)

        // Act
        var result = _unitsService.GetChainString(unit);

        // Assert
        result.Should().Be("1 Section, SFSG, UKSF");
    }
}
