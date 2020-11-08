using System;
using FluentAssertions;
using UKSF.Api.Base.Models.Logging;
using Xunit;

namespace UKSF.Tests.Unit.Models.Message.Logging {
    public class BasicLogMessageTests {
        [Fact]
        public void ShouldSetText() {
            BasicLog subject = new BasicLog("test");

            subject.message.Should().Be("test");
        }

        [Fact]
        public void ShouldSetTextAndLogLevel() {
            BasicLog subject = new BasicLog("test", LogLevel.DEBUG);

            subject.message.Should().Be("test");
            subject.level.Should().Be(LogLevel.DEBUG);
        }

        [Fact]
        public void ShouldSetTextAndLogLevelFromException() {
            BasicLog subject = new BasicLog(new Exception("test"));

            subject.message.Should().Be("System.Exception: test");
            subject.level.Should().Be(LogLevel.ERROR);
        }
    }
}
