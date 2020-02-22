using System;
using FluentAssertions;
using UKSF.Api.Models.Message.Logging;
using Xunit;

namespace UKSF.Tests.Unit.Models.Message.Logging {
    public class LauncherLogMessageTests {
        [Fact]
        public void ShouldSetVersionAndMessage() {
            LauncherLogMessage subject = new LauncherLogMessage("1.0.0", "test");

            subject.message.Should().Be("test");
            subject.version.Should().Be("1.0.0");
        }
    }
}
