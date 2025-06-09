using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Controllers;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Mappers;
using UKSF.Api.Queries;
using Xunit;

namespace UKSF.Api.Tests.Controllers;

public class UnitsControllerTests
{
    private readonly Mock<IUnitsContext> _mockUnitsContext;
    private readonly Mock<IUnitsService> _mockUnitsService;
    private readonly Mock<IGetUnitTreeQuery> _mockGetUnitTreeQuery;
    private readonly Mock<IUnitTreeMapper> _mockUnitTreeMapper;
    private readonly UnitsController _controller;

    private readonly string _accountId = ObjectId.GenerateNewId().ToString();

    public UnitsControllerTests()
    {
        var mockAccountContext = new Mock<IAccountContext>();
        _mockUnitsContext = new Mock<IUnitsContext>();
        _mockUnitsService = new Mock<IUnitsService>();
        var mockAssignmentService = new Mock<IAssignmentService>();
        var mockNotificationsService = new Mock<INotificationsService>();
        var mockLogger = new Mock<IUksfLogger>();
        _mockGetUnitTreeQuery = new Mock<IGetUnitTreeQuery>();
        _mockUnitTreeMapper = new Mock<IUnitTreeMapper>();

        _controller = new UnitsController(
            mockAccountContext.Object,
            _mockUnitsContext.Object,
            _mockUnitsService.Object,
            mockAssignmentService.Object,
            mockNotificationsService.Object,
            mockLogger.Object,
            _mockGetUnitTreeQuery.Object,
            _mockUnitTreeMapper.Object
        );
    }

    [Fact]
    public void Get_Should_Filter_Secondary_Units_With_AccountId()
    {
        // Arrange
        var secondaryUnits = new List<DomainUnit>
        {
            new()
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Name = "Secondary Unit 1",
                Branch = UnitBranch.Secondary
            },
            new()
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Name = "Secondary Unit 2",
                Branch = UnitBranch.Secondary
            }
        };

        _mockUnitsService.Setup(x => x.GetSortedUnits(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns<Func<DomainUnit, bool>>(predicate => secondaryUnits.Where(predicate));

        // Act
        var result = _controller.Get("secondary", _accountId);

        // Assert
        _mockUnitsService.Verify(x => x.GetSortedUnits(It.IsAny<Func<DomainUnit, bool>>()), Times.Once);
        result.Should().NotBeNull();
    }

    [Fact]
    public void Get_Should_Filter_Secondary_Units_Without_AccountId()
    {
        // Arrange
        var secondaryUnits = new List<DomainUnit>
        {
            new()
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Name = "Secondary Unit 1",
                Branch = UnitBranch.Secondary
            },
            new()
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Name = "Secondary Unit 2",
                Branch = UnitBranch.Secondary
            }
        };

        _mockUnitsService.Setup(x => x.GetSortedUnits(It.IsAny<Func<DomainUnit, bool>>()))
                         .Returns<Func<DomainUnit, bool>>(predicate => secondaryUnits.Where(predicate));

        // Act
        var result = _controller.Get("secondary");

        // Assert
        _mockUnitsService.Verify(x => x.GetSortedUnits(It.IsAny<Func<DomainUnit, bool>>()), Times.Once);
        result.Should().NotBeNull();
    }

    [Fact]
    public void GetUnitsChart_Should_Support_Secondary_Type()
    {
        // Arrange
        var secondaryRoot = new DomainUnit
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Secondary Root",
            Shortname = "SEC",
            Branch = UnitBranch.Secondary,
            Parent = ObjectId.Empty.ToString(),
            PreferShortname = false
        };

        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns(secondaryRoot);

        _mockUnitsService.Setup(x => x.MapUnitMembers(It.IsAny<DomainUnit>())).Returns(new List<UnitMemberDto>());

        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns(new List<DomainUnit>());

        // Act
        var result = _controller.GetUnitsChart("secondary");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(secondaryRoot.Id);
        result.Name.Should().Be("Secondary Root");
    }

    [Fact]
    public async Task GetTree_Should_Include_Secondary_Nodes()
    {
        // Arrange
        var combatTree = new DomainUnit { Name = "Combat Root", Branch = UnitBranch.Combat };
        var auxiliaryTree = new DomainUnit { Name = "Auxiliary Root", Branch = UnitBranch.Auxiliary };
        var secondaryTree = new DomainUnit { Name = "Secondary Root", Branch = UnitBranch.Secondary };

        var combatTreeNode = new UnitTreeNodeDto { Name = "Combat Node" };
        var auxiliaryTreeNode = new UnitTreeNodeDto { Name = "Auxiliary Node" };
        var secondaryTreeNode = new UnitTreeNodeDto { Name = "Secondary Node" };

        _mockGetUnitTreeQuery.Setup(x => x.ExecuteAsync(It.Is<GetUnitTreeQueryArgs>(args => args.UnitBranch == UnitBranch.Combat))).ReturnsAsync(combatTree);
        _mockGetUnitTreeQuery.Setup(x => x.ExecuteAsync(It.Is<GetUnitTreeQueryArgs>(args => args.UnitBranch == UnitBranch.Auxiliary)))
                             .ReturnsAsync(auxiliaryTree);
        _mockGetUnitTreeQuery.Setup(x => x.ExecuteAsync(It.Is<GetUnitTreeQueryArgs>(args => args.UnitBranch == UnitBranch.Secondary)))
                             .ReturnsAsync(secondaryTree);

        _mockUnitTreeMapper.Setup(x => x.MapUnitTree(combatTree)).Returns(combatTreeNode);
        _mockUnitTreeMapper.Setup(x => x.MapUnitTree(auxiliaryTree)).Returns(auxiliaryTreeNode);
        _mockUnitTreeMapper.Setup(x => x.MapUnitTree(secondaryTree)).Returns(secondaryTreeNode);

        // Act
        var result = await _controller.GetTree();

        // Assert
        result.Should().NotBeNull();
        result.CombatNodes.Should().ContainSingle().Which.Should().Be(combatTreeNode);
        result.AuxiliaryNodes.Should().ContainSingle().Which.Should().Be(auxiliaryTreeNode);
        result.SecondaryNodes.Should().ContainSingle().Which.Should().Be(secondaryTreeNode);

        _mockGetUnitTreeQuery.Verify(x => x.ExecuteAsync(It.Is<GetUnitTreeQueryArgs>(args => args.UnitBranch == UnitBranch.Combat)), Times.Once);
        _mockGetUnitTreeQuery.Verify(x => x.ExecuteAsync(It.Is<GetUnitTreeQueryArgs>(args => args.UnitBranch == UnitBranch.Auxiliary)), Times.Once);
        _mockGetUnitTreeQuery.Verify(x => x.ExecuteAsync(It.Is<GetUnitTreeQueryArgs>(args => args.UnitBranch == UnitBranch.Secondary)), Times.Once);
    }
}
