using System;
using FluentAssertions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models.Domain;
using Xunit;

namespace UKSF.Api.Core.Tests.Extensions;

public class VariablesExtensionsTests
{
    [Fact]
    public void AsArray_Should_Throw_When_Item_Is_Null()
    {
        var variable = new DomainVariableItem { Key = "SOME_KEY", Item = null };

        var act = () => variable.AsArray();

        act.Should().Throw<Exception>().WithMessage("Variable SOME_KEY has no item");
    }

    [Fact]
    public void AsBoolWithDefault_Should_Return_The_Given_Default_When_Item_Is_Null()
    {
        var variable = new DomainVariableItem { Key = "SOME_KEY", Item = null };

        variable.AsBoolWithDefault(true).Should().BeTrue();
        variable.AsBoolWithDefault(false).Should().BeFalse();
    }

    [Fact]
    public void AsBoolWithDefault_Should_Return_The_Item_When_Set()
    {
        var variable = new DomainVariableItem { Key = "SOME_KEY", Item = "false" };

        variable.AsBoolWithDefault(true).Should().BeFalse();
    }

    [Fact]
    public void AsUlongWithDefault_Should_Return_Default_When_Unset_Or_Unparsable()
    {
        new DomainVariableItem { Key = "SOME_KEY", Item = null }.AsUlongWithDefault(42).Should().Be(42);
        new DomainVariableItem { Key = "SOME_KEY", Item = "nonsense" }.AsUlongWithDefault(42).Should().Be(42);
        new DomainVariableItem { Key = "SOME_KEY", Item = "707615025380065400" }.AsUlongWithDefault(42).Should().Be(707615025380065400);
    }

    [Fact]
    public void AsStringWithDefault_Should_Return_Default_When_Item_Is_Null()
    {
        var variable = new DomainVariableItem { Key = "SOME_KEY", Item = null };

        variable.AsStringWithDefault("fallback").Should().Be("fallback");
    }

    [Fact]
    public void AsStringWithDefault_Should_Return_Default_When_Variable_Is_Null()
    {
        DomainVariableItem variable = null;

        variable.AsStringWithDefault("fallback").Should().Be("fallback");
    }

    [Fact]
    public void AsStringWithDefault_Should_Return_Item_When_Set()
    {
        var variable = new DomainVariableItem { Key = "SOME_KEY", Item = "value" };

        variable.AsStringWithDefault("fallback").Should().Be("value");
    }

    [Fact]
    public void AsArrayWithDefault_Should_Return_Empty_Array_When_Item_Is_Null()
    {
        var variable = new DomainVariableItem { Key = "SOME_KEY", Item = null };

        var result = variable.AsArrayWithDefault();

        result.Should().BeEmpty();
    }

    [Fact]
    public void AsArrayWithDefault_Should_Return_Empty_Array_When_Variable_Is_Null()
    {
        DomainVariableItem variable = null;

        var result = variable.AsArrayWithDefault();

        result.Should().BeEmpty();
    }

    [Fact]
    public void AsArrayWithDefault_Should_Return_Items_When_Item_Is_Set()
    {
        var variable = new DomainVariableItem { Key = "SOME_KEY", Item = "one, two,three" };

        var result = variable.AsArrayWithDefault();

        result.Should().BeEquivalentTo(["one", "two", "three"]);
    }
}
