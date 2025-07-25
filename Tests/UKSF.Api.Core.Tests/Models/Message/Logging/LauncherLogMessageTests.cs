using FluentAssertions;
using UKSF.Api.Core.Models;
using Xunit;

namespace UKSF.Api.Core.Tests.Models.Message.Logging;

public class LauncherLogMessageTests
{
    [Fact]
    public void ShouldSetVersionAndMessage()
    {
        LauncherLog subject = new("1.0.0", "test");

        subject.Message.Should().Be("test");
        subject.Version.Should().Be("1.0.0");
    }
}
