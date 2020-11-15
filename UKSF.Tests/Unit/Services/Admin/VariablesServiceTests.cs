using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Models;
using UKSF.Api.Shared.Extensions;
using Xunit;

namespace UKSF.Tests.Unit.Services.Admin {
    public class VariablesServiceTests {
        [Fact]
        public void ShouldGetVariableAsString() {
            const string EXPECTED = "Value";
            VariableItem variableItem = new VariableItem {key = "Test", item = EXPECTED};

            string subject = variableItem.AsString();

            subject.Should().Be(EXPECTED);
        }

        [Fact]
        public void ShouldGetVariableAsDouble() {
            const double EXPECTED = 1.5;
            VariableItem variableItem = new VariableItem {key = "Test", item = EXPECTED};

            double subject = variableItem.AsDouble();

            subject.Should().Be(EXPECTED);
        }

        [Fact]
        public void ShouldGetVariableAsBool() {
            const bool EXPECTED = true;
            VariableItem variableItem = new VariableItem {key = "Test", item = EXPECTED};

            bool subject = variableItem.AsBool();

            subject.Should().Be(EXPECTED);
        }

        [Fact]
        public void ShouldGetVariableAsUlong() {
            const ulong EXPECTED = ulong.MaxValue;
            VariableItem variableItem = new VariableItem {key = "Test", item = EXPECTED};

            ulong subject = variableItem.AsUlong();

            subject.Should().Be(EXPECTED);
        }

        [Fact]
        public void ShouldGetVariableAsArray() {
            VariableItem variableItem = new VariableItem {key = "Test", item = "item1,item2, item3"};

            string[] subject = variableItem.AsArray();

            subject.Should().HaveCount(3);
            subject.Should().Contain(new[] {"item1", "item2", "item3"});
        }

        // ReSharper disable PossibleMultipleEnumeration
        [Fact]
        public void ShouldGetVariableAsEnumerable() {
            VariableItem variableItem = new VariableItem {key = "Test", item = "item1,item2, item3"};

            IEnumerable<string> subject = variableItem.AsEnumerable();

            subject.Should().BeAssignableTo<IEnumerable<string>>();
            subject.Should().HaveCount(3);
            subject.Should().Contain(new[] {"item1", "item2", "item3"});
        }
        // ReSharper restore PossibleMultipleEnumeration

        [Fact]
        public void ShouldGetVariableAsArrayWithPredicate() {
            VariableItem variableItem = new VariableItem {key = "Test", item = "\"item1\",item2"};

            string[] subject = variableItem.AsArray(x => x.RemoveQuotes());

            subject.Should().HaveCount(2);
            subject.Should().Contain(new[] {"item1", "item2"});
        }

        [Fact]
        public void ShouldGetVariableAsDoublesArray() {
            VariableItem variableItem = new VariableItem {key = "Test", item = "1.5,1.67845567657, -0.000000456"};

            List<double> subject = variableItem.AsDoublesArray().ToList();

            subject.Should().HaveCount(3);
            subject.Should().Contain(new[] {1.5, 1.67845567657, -0.000000456});
        }

        [Fact]
        public void ShouldHaveItem() {
            VariableItem variableItem = new VariableItem {key = "Test", item = "test"};

            Action act = () => variableItem.AssertHasItem();

            act.Should().NotThrow();
        }

        [Fact]
        public void ShouldThrowWithNoItem() {
            VariableItem variableItem = new VariableItem {key = "Test"};

            Action act = () => variableItem.AssertHasItem();

            act.Should().Throw<Exception>();
        }

        [Fact]
        public void ShouldThrowWithInvalidDouble() {
            VariableItem variableItem = new VariableItem {key = "Test", item = "wontwork"};

            Action act = () => variableItem.AsDouble();

            act.Should().Throw<InvalidCastException>();
        }

        [Fact]
        public void ShouldThrowWithInvalidBool() {
            VariableItem variableItem = new VariableItem {key = "Test", item = "wontwork"};

            Action act = () => variableItem.AsBool();

            act.Should().Throw<InvalidCastException>();
        }

        [Fact]
        public void ShouldThrowWithInvalidUlong() {
            VariableItem variableItem = new VariableItem {key = "Test", item = "wontwork"};

            Action act = () => variableItem.AsUlong();

            act.Should().Throw<InvalidCastException>();
        }
    }
}
