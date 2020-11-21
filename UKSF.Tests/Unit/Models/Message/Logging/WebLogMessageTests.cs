using System;
using FluentAssertions;
using UKSF.Api.Shared.Models;
using Xunit;

namespace UKSF.Tests.Unit.Models.Message.Logging {
    public class WebLogMessageTests {
        [Fact]
        public void ShouldCreateFromException() {
            HttpErrorLog subject = new(new Exception("test"));

            subject.Message.Should().Be("test");
            subject.Exception.Should().Be("System.Exception: test");
            subject.Level.Should().Be(LogLevel.ERROR);
        }
    }
}
