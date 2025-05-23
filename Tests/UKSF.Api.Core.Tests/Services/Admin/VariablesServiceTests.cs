using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models.Domain;
using Xunit;

namespace UKSF.Api.Core.Tests.Services.Admin;

public class VariablesServiceTests
{
    [Fact]
    public void ShouldGetVariableAsArray()
    {
        DomainVariableItem variableItem = new() { Key = "Test", Item = "item1,item2, item3" };

        var subject = variableItem.AsArray();

        subject.Should().HaveCount(3);
        subject.Should().Contain(["item1", "item2", "item3"]);
    }

    [Fact]
    public void ShouldGetVariableAsArrayWithPredicate()
    {
        DomainVariableItem variableItem = new() { Key = "Test", Item = "\"item1\",item2" };

        var subject = variableItem.AsArray(x => x.RemoveQuotes());

        subject.Should().HaveCount(2);
        subject.Should().Contain(["item1", "item2"]);
    }

    [Fact]
    public void ShouldGetVariableAsBool()
    {
        const bool Expected = true;
        DomainVariableItem variableItem = new()
        {
            Key = "Test",
            Item = Expected
        };

        var subject = variableItem.AsBool();

        subject.Should().Be(Expected);
    }

    [Fact]
    public void ShouldGetVariableAsDouble()
    {
        const double Expected = 1.5;
        DomainVariableItem variableItem = new()
        {
            Key = "Test",
            Item = Expected
        };

        var subject = variableItem.AsDouble();

        subject.Should().Be(Expected);
    }

    [Fact]
    public void ShouldGetVariableAsDoublesArray()
    {
        DomainVariableItem variableItem = new() { Key = "Test", Item = "1.5,1.67845567657, -0.000000456" };

        var subject = variableItem.AsDoublesArray().ToList();

        subject.Should().HaveCount(3);
        subject.Should().Contain([1.5, 1.67845567657, -0.000000456]);
    }

    // ReSharper disable PossibleMultipleEnumeration
    [Fact]
    public void ShouldGetVariableAsEnumerable()
    {
        DomainVariableItem variableItem = new() { Key = "Test", Item = "item1,item2, item3" };

        var subject = variableItem.AsEnumerable();

        subject.Should().BeAssignableTo<IEnumerable<string>>();
        subject.Should().HaveCount(3);
        subject.Should().Contain(["item1", "item2", "item3"]);
    }
    // ReSharper restore PossibleMultipleEnumeration

    [Fact]
    public void ShouldGetVariableAsString()
    {
        const string Expected = "Value";
        DomainVariableItem variableItem = new()
        {
            Key = "Test",
            Item = Expected
        };

        var subject = variableItem.AsString();

        subject.Should().Be(Expected);
    }

    [Fact]
    public void ShouldGetVariableAsUlong()
    {
        const ulong Expected = ulong.MaxValue;
        DomainVariableItem variableItem = new()
        {
            Key = "Test",
            Item = Expected
        };

        var subject = variableItem.AsUlong();

        subject.Should().Be(Expected);
    }

    [Fact]
    public void ShouldHaveItem()
    {
        DomainVariableItem variableItem = new() { Key = "Test", Item = "test" };

        Action act = () => variableItem.AssertHasItem();

        act.Should().NotThrow();
    }

    [Fact]
    public void ShouldThrowWithInvalidBool()
    {
        DomainVariableItem variableItem = new() { Key = "Test", Item = "wontwork" };

        Action act = () => variableItem.AsBool();

        act.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void ShouldThrowWithInvalidDouble()
    {
        DomainVariableItem variableItem = new() { Key = "Test", Item = "wontwork" };

        Action act = () => variableItem.AsDouble();

        act.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void ShouldThrowWithInvalidUlong()
    {
        DomainVariableItem variableItem = new() { Key = "Test", Item = "wontwork" };

        Action act = () => variableItem.AsUlong();

        act.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void ShouldThrowWithNoItem()
    {
        DomainVariableItem variableItem = new() { Key = "Test" };

        Action act = () => variableItem.AssertHasItem();

        act.Should().Throw<Exception>();
    }
}
