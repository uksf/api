using FluentAssertions;
using UKSF.Api.Shared.Models;
using Xunit;

namespace UKSF.Tests.Unit.Models.Message.Logging;

public class WebLogMessageTests
{
    [Fact]
    public void ShouldCreateFromException()
    {
        ErrorLog subject = new(new("test"), "url", "method", "endpoint", 500, "userId", "userName");

        subject.Message.Should().Be("test");
        subject.Exception.Should().Be("System.Exception: test");
        subject.Level.Should().Be(LogLevel.ERROR);
        subject.Url.Should().Be("url");
        subject.Method.Should().Be("method");
        subject.EndpointName.Should().Be("endpoint");
        subject.StatusCode.Should().Be(500);
        subject.UserId.Should().Be("userId");
        subject.Name.Should().Be("userName");
    }
}
