using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Models;
using UKSF.Api.Shared.Extensions;
using Xunit;

namespace UKSF.Tests.Unit.Services.Admin
{
    public class VariablesServiceTests
    {
        [Fact]
        public void ShouldGetVariableAsArray()
        {
            VariableItem variableItem = new() { Key = "Test", Item = "item1,item2, item3" };

            string[] subject = variableItem.AsArray();

            subject.Should().HaveCount(3);
            subject.Should().Contain(new[] { "item1", "item2", "item3" });
        }

        [Fact]
        public void ShouldGetVariableAsArrayWithPredicate()
        {
            VariableItem variableItem = new() { Key = "Test", Item = "\"item1\",item2" };

            string[] subject = variableItem.AsArray(x => x.RemoveQuotes());

            subject.Should().HaveCount(2);
            subject.Should().Contain(new[] { "item1", "item2" });
        }

        [Fact]
        public void ShouldGetVariableAsBool()
        {
            const bool EXPECTED = true;
            VariableItem variableItem = new() { Key = "Test", Item = EXPECTED };

            bool subject = variableItem.AsBool();

            subject.Should().Be(EXPECTED);
        }

        [Fact]
        public void ShouldGetVariableAsDouble()
        {
            const double EXPECTED = 1.5;
            VariableItem variableItem = new() { Key = "Test", Item = EXPECTED };

            double subject = variableItem.AsDouble();

            subject.Should().Be(EXPECTED);
        }

        [Fact]
        public void ShouldGetVariableAsDoublesArray()
        {
            VariableItem variableItem = new() { Key = "Test", Item = "1.5,1.67845567657, -0.000000456" };

            List<double> subject = variableItem.AsDoublesArray().ToList();

            subject.Should().HaveCount(3);
            subject.Should().Contain(new[] { 1.5, 1.67845567657, -0.000000456 });
        }

        // ReSharper disable PossibleMultipleEnumeration
        [Fact]
        public void ShouldGetVariableAsEnumerable()
        {
            VariableItem variableItem = new() { Key = "Test", Item = "item1,item2, item3" };

            IEnumerable<string> subject = variableItem.AsEnumerable();

            subject.Should().BeAssignableTo<IEnumerable<string>>();
            subject.Should().HaveCount(3);
            subject.Should().Contain(new[] { "item1", "item2", "item3" });
        }
        // ReSharper restore PossibleMultipleEnumeration

        [Fact]
        public void ShouldGetVariableAsString()
        {
            const string EXPECTED = "Value";
            VariableItem variableItem = new() { Key = "Test", Item = EXPECTED };

            string subject = variableItem.AsString();

            subject.Should().Be(EXPECTED);
        }

        [Fact]
        public void ShouldGetVariableAsUlong()
        {
            const ulong EXPECTED = ulong.MaxValue;
            VariableItem variableItem = new() { Key = "Test", Item = EXPECTED };

            ulong subject = variableItem.AsUlong();

            subject.Should().Be(EXPECTED);
        }

        [Fact]
        public void ShouldHaveItem()
        {
            VariableItem variableItem = new() { Key = "Test", Item = "test" };

            Action act = () => variableItem.AssertHasItem();

            act.Should().NotThrow();
        }

        [Fact]
        public void ShouldThrowWithInvalidBool()
        {
            VariableItem variableItem = new() { Key = "Test", Item = "wontwork" };

            Action act = () => variableItem.AsBool();

            act.Should().Throw<InvalidCastException>();
        }

        [Fact]
        public void ShouldThrowWithInvalidDouble()
        {
            VariableItem variableItem = new() { Key = "Test", Item = "wontwork" };

            Action act = () => variableItem.AsDouble();

            act.Should().Throw<InvalidCastException>();
        }

        [Fact]
        public void ShouldThrowWithInvalidUlong()
        {
            VariableItem variableItem = new() { Key = "Test", Item = "wontwork" };

            Action act = () => variableItem.AsUlong();

            act.Should().Throw<InvalidCastException>();
        }

        [Fact]
        public void ShouldThrowWithNoItem()
        {
            VariableItem variableItem = new() { Key = "Test" };

            Action act = () => variableItem.AssertHasItem();

            act.Should().Throw<Exception>();
        }
    }
}
