using System;
using System.Linq;
using FluentAssertions;
using UKSF.Api.Shared.Extensions;
using Xunit;

namespace UKSF.Tests.Unit.Common
{
    public class StringUtilitiesTests
    {
        [Theory]
        [InlineData("", "", false)]
        [InlineData("", "hello", false)]
        [InlineData("hello world hello world", "hello", true)]
        [InlineData("hello", "HELLO", true)]
        [InlineData("hello world", "HELLOWORLD", false)]
        public void ShouldIgnoreCase(string text, string searchElement, bool expected)
        {
            var subject = text.ContainsIgnoreCase(searchElement);

            subject.Should().Be(expected);
        }

        [Theory]
        [InlineData("")]
        [InlineData("2")]
        [InlineData("1E+309")]
        [InlineData("-1E+309")] // E+309 is one more than double max/min
        public void ShouldNotThrowExceptionForDouble(string text)
        {
            Action act = () => text.ToDouble();

            act.Should().NotThrow();
        }

        [Theory]
        [InlineData("", 0)]
        [InlineData("2", 2)]
        [InlineData("1.79769313486232E+307", 1.79769313486232E+307d)]
        [InlineData("-1.79769313486232E+307", -1.79769313486232E+307d)] // E+307 is one less than double max/min
        public void ShouldParseDoubleCorrectly(string text, double expected)
        {
            var subject = text.ToDouble();

            subject.Should().Be(expected);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("hello", "Hello")]
        [InlineData("hi there my name is bob", "Hi There My Name Is Bob")]
        [InlineData("HELLO BOB", "HELLO BOB")]
        public void ShouldConvertToTitleCase(string text, string expected)
        {
            var subject = text.ToTitleCase();

            subject.Should().Be(expected);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("hello world", "HELLO_WORLD")]
        [InlineData("HELLO_WORLD", "HELLO_WORLD")]
        [InlineData("  i am key   ", "I_AM_KEY")]
        public void ShouldKeyify(string text, string expected)
        {
            var subject = text.Keyify();

            subject.Should().Be(expected);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("hello world hello world", "helloworldhelloworld")]
        [InlineData("hello", "hello")]
        [InlineData("  hello world   ", "helloworld")]
        public void ShouldRemoveSpaces(string text, string expected)
        {
            var subject = text.RemoveSpaces();

            subject.Should().Be(expected);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("\"hello world;\" \\n \"\";", "\"hello world;\";")]
        public void ShouldRemoveTrailingNewLineGroup(string text, string expected)
        {
            var subject = text.RemoveTrailingNewLineGroup();

            subject.Should().Be(expected);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("hello\\nworld\\n\\nhello world", "helloworldhello world")]
        [InlineData("hello\\n", "hello")]
        [InlineData("\\n  hello world   \\n", "  hello world   ")]
        public void ShouldRemoveNewLines(string text, string expected)
        {
            var subject = text.RemoveNewLines();

            subject.Should().Be(expected);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("\"helloworld\" \"hello world\"", "helloworld hello world")]
        [InlineData("hello\"\"", "hello")]
        [InlineData("\"  hello world   \"", "  hello world   ")]
        public void ShouldRemoveQuotes(string text, string expected)
        {
            var subject = text.RemoveQuotes();

            subject.Should().Be(expected);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("\"hello \"\"test\"\" world\"", "\"hello 'test' world\"")]
        [InlineData("\"hello \" \"test\"\" world\"", "\"hello test' world\"")]
        [InlineData("\"\"\"\"", "''")]
        public void ShouldRemoveEmbeddedQuotes(string text, string expected)
        {
            var subject = text.RemoveEmbeddedQuotes();

            subject.Should().Be(expected);
        }

        [Theory]
        [InlineData("Hello I am 5e39336e1b92ee2d14b7fe08", "5e39336e1b92ee2d14b7fe08")]
        [InlineData("Hello I am 5e39336e1b92ee2d14b7fe08, I will be your SR1", "5e39336e1b92ee2d14b7fe08")]
        public void ShouldExtractObjectIds(string input, string expected)
        {
            var subject = input.ExtractObjectIds().ToList();

            subject.Should().Contain(expected);
        }
    }
}
