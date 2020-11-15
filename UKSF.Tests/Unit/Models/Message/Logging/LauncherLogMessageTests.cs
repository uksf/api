using FluentAssertions;
using UKSF.Api.Shared.Models;
using Xunit;

namespace UKSF.Tests.Unit.Models.Message.Logging {
    public class LauncherLogMessageTests {
        [Fact]
        public void ShouldSetVersionAndMessage() {
            LauncherLog subject = new LauncherLog("1.0.0", "test");

            subject.message.Should().Be("test");
            subject.version.Should().Be("1.0.0");
        }
    }
}
