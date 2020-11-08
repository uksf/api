using System;
using FluentAssertions;
using UKSF.Api.Base.Models.Logging;
using Xunit;

namespace UKSF.Tests.Unit.Models.Message.Logging {
    public class WebLogMessageTests {
        [Fact]
        public void ShouldCreateFromException() {
            HttpErrorLog subject = new HttpErrorLog(new Exception("test"));

            subject.message.Should().Be("test");
            subject.exception.Should().Be("System.Exception: test");
            subject.level.Should().Be(LogLevel.ERROR);
        }
    }
}
