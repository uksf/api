using System;
using FluentAssertions;
using UKSF.Api.Models.Message.Logging;
using Xunit;

namespace UKSF.Tests.Unit.Models.Message.Logging {
    public class BasicLogMessageTests {
        [Fact]
        public void ShouldSetText() {
            BasicLogMessage subject = new BasicLogMessage("test");

            subject.message.Should().Be("test");
        }

        [Fact]
        public void ShouldSetLogLevel() {
            BasicLogMessage subject = new BasicLogMessage(LogLevel.DEBUG);

            subject.level.Should().Be(LogLevel.DEBUG);
        }

        [Fact]
        public void ShouldSetTextAndLogLevel() {
            BasicLogMessage subject = new BasicLogMessage("test", LogLevel.DEBUG);

            subject.message.Should().Be("test");
            subject.level.Should().Be(LogLevel.DEBUG);
        }

        [Fact]
        public void ShouldSetTextAndLogLevelFromException() {
            BasicLogMessage subject = new BasicLogMessage(new Exception("test"));

            subject.message.Should().Be("System.Exception: test");
            subject.level.Should().Be(LogLevel.ERROR);
        }
    }
}
