using System;
using FluentAssertions;
using UKSF.Api.Core.Models;
using Xunit;

namespace UKSF.Tests.Unit.Models.Message.Logging;

public class BasicLogMessageTests
{
    [Fact]
    public void ShouldSetText()
    {
        BasicLog subject = new("test");

        subject.Message.Should().Be("test");
    }

    [Fact]
    public void ShouldSetTextAndLogLevel()
    {
        BasicLog subject = new("test", UksfLogLevel.Debug);

        subject.Message.Should().Be("test");
        subject.Level.Should().Be(UksfLogLevel.Debug);
    }

    [Fact]
    public void ShouldSetTextAndLogLevelFromException()
    {
        BasicLog subject = new(new Exception("test"));

        subject.Message.Should().Be("System.Exception: test");
        subject.Level.Should().Be(UksfLogLevel.Error);
    }
}
