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
