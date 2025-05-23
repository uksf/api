using System;
using FluentAssertions;
using UKSF.Api.Core.Models.Domain;
using Xunit;

namespace UKSF.Api.Core.Tests.Models.Message.Logging;

public class BasicLogMessageTests
{
    [Fact]
    public void ShouldSetText()
    {
        DomainBasicLog subject = new("test");

        subject.Message.Should().Be("test");
    }

    [Fact]
    public void ShouldSetTextAndLogLevel()
    {
        DomainBasicLog subject = new("test", UksfLogLevel.Debug);

        subject.Message.Should().Be("test");
        subject.Level.Should().Be(UksfLogLevel.Debug);
    }

    [Fact]
    public void ShouldSetTextAndLogLevelFromException()
    {
        DomainBasicLog subject = new(new Exception("test"));

        subject.Message.Should().Be("System.Exception: test");
        subject.Level.Should().Be(UksfLogLevel.Error);
    }
}
