using System;
using FluentAssertions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using Xunit;

namespace UKSF.Api.Core.Tests.Models.Message.Logging;

public class WebLogMessageTests
{
    [Fact]
    public void ShouldCreateFromException()
    {
        ErrorLog subject = new(new Exception("test"), "url", "method", "endpoint", 500, "userId", "userName");

        subject.Message.Should().Be("test");
        subject.Exception.Should().Be("System.Exception: test");
        subject.Level.Should().Be(UksfLogLevel.Error);
        subject.Url.Should().Be("url");
        subject.Method.Should().Be("method");
        subject.EndpointName.Should().Be("endpoint");
        subject.StatusCode.Should().Be(500);
        subject.UserId.Should().Be("userId");
        subject.Name.Should().Be("userName");
    }
}
