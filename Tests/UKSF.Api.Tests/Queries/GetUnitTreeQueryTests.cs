using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Queries;
using Xunit;

namespace UKSF.Api.Tests.Queries;

public class GetUnitTreeQueryTests
{
    private readonly Mock<IUnitsContext> _mockUnitsContext;
    private readonly GetUnitTreeQuery _subject;

    public GetUnitTreeQueryTests()
    {
        _mockUnitsContext = new Mock<IUnitsContext>();
        _subject = new GetUnitTreeQuery(_mockUnitsContext.Object);
    }

    private void SetupUnitsContext(List<DomainUnit> allUnits)
    {
        _mockUnitsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(pred => allUnits.SingleOrDefault(pred));
        _mockUnitsContext.Setup(x => x.Get(It.IsAny<Func<DomainUnit, bool>>())).Returns<Func<DomainUnit, bool>>(pred => allUnits.Where(pred));
    }

    [Fact]
    public async Task ExecuteAsync_returns_root_unit_for_branch()
    {
        var rootId = ObjectId.GenerateNewId().ToString();
        var root = new DomainUnit
        {
            Id = rootId,
            Name = "Root",
            Parent = ObjectId.Empty.ToString(),
            Branch = UnitBranch.Combat
        };
        SetupUnitsContext(new List<DomainUnit> { root });

        var result = await _subject.ExecuteAsync(new GetUnitTreeQueryArgs(UnitBranch.Combat));

        result.Should().BeSameAs(root);
        result.Name.Should().Be("Root");
        result.Children.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_populates_direct_children()
    {
        var rootId = ObjectId.GenerateNewId().ToString();
        var root = new DomainUnit
        {
            Id = rootId,
            Name = "Root",
            Parent = ObjectId.Empty.ToString(),
            Branch = UnitBranch.Combat
        };
        var child1 = new DomainUnit
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Child1",
            Parent = rootId,
            Branch = UnitBranch.Combat
        };
        var child2 = new DomainUnit
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Child2",
            Parent = rootId,
            Branch = UnitBranch.Combat
        };
        SetupUnitsContext(
            new List<DomainUnit>
            {
                root,
                child1,
                child2
            }
        );

        var result = await _subject.ExecuteAsync(new GetUnitTreeQueryArgs(UnitBranch.Combat));

        result.Children.Should().HaveCount(2);
        result.Children.Should().Contain(child1);
        result.Children.Should().Contain(child2);
    }

    [Fact]
    public async Task ExecuteAsync_populates_nested_children_recursively()
    {
        var rootId = ObjectId.GenerateNewId().ToString();
        var childId = ObjectId.GenerateNewId().ToString();
        var root = new DomainUnit
        {
            Id = rootId,
            Name = "Root",
            Parent = ObjectId.Empty.ToString(),
            Branch = UnitBranch.Combat
        };
        var child = new DomainUnit
        {
            Id = childId,
            Name = "Child",
            Parent = rootId,
            Branch = UnitBranch.Combat
        };
        var grandchild = new DomainUnit
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Grandchild",
            Parent = childId,
            Branch = UnitBranch.Combat
        };
        SetupUnitsContext(
            new List<DomainUnit>
            {
                root,
                child,
                grandchild
            }
        );

        var result = await _subject.ExecuteAsync(new GetUnitTreeQueryArgs(UnitBranch.Combat));

        result.Children.Should().HaveCount(1);
        result.Children[0].Name.Should().Be("Child");
        result.Children[0].Children.Should().HaveCount(1);
        result.Children[0].Children[0].Name.Should().Be("Grandchild");
    }

    [Fact]
    public async Task ExecuteAsync_only_includes_children_matching_parent_id()
    {
        var rootId = ObjectId.GenerateNewId().ToString();
        var unrelatedId = ObjectId.GenerateNewId().ToString();
        var root = new DomainUnit
        {
            Id = rootId,
            Name = "Root",
            Parent = ObjectId.Empty.ToString(),
            Branch = UnitBranch.Combat
        };
        var child = new DomainUnit
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Child",
            Parent = rootId,
            Branch = UnitBranch.Combat
        };
        var unrelated = new DomainUnit
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Unrelated",
            Parent = unrelatedId,
            Branch = UnitBranch.Combat
        };
        SetupUnitsContext(
            new List<DomainUnit>
            {
                root,
                child,
                unrelated
            }
        );

        var result = await _subject.ExecuteAsync(new GetUnitTreeQueryArgs(UnitBranch.Combat));

        result.Children.Should().HaveCount(1);
        result.Children[0].Name.Should().Be("Child");
    }

    [Fact]
    public async Task ExecuteAsync_returns_empty_children_for_leaf_nodes()
    {
        var rootId = ObjectId.GenerateNewId().ToString();
        var root = new DomainUnit
        {
            Id = rootId,
            Name = "Root",
            Parent = ObjectId.Empty.ToString(),
            Branch = UnitBranch.Combat
        };
        var leaf = new DomainUnit
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = "Leaf",
            Parent = rootId,
            Branch = UnitBranch.Combat
        };
        SetupUnitsContext(new List<DomainUnit> { root, leaf });

        var result = await _subject.ExecuteAsync(new GetUnitTreeQueryArgs(UnitBranch.Combat));

        result.Children.Should().HaveCount(1);
        result.Children[0].Children.Should().BeEmpty();
    }
}
