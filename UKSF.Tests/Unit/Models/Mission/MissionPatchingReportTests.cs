using System;
using FluentAssertions;
using UKSF.Api.Models.Mission;
using Xunit;

namespace UKSF.Tests.Unit.Unit.Models.Mission {
    public class MissionPatchingReportTests {
        [Fact]
        public void ShouldSetFieldsAsError() {
            MissionPatchingReport subject = new MissionPatchingReport("Test Title", "Test details, like what went wrong, what needs to be done to fix it", true);

            subject.title.Should().Be("Error: Test Title");
            subject.detail.Should().Be("Test details, like what went wrong, what needs to be done to fix it");
            subject.error.Should().BeTrue();
        }

        [Fact]
        public void ShouldSetFieldsAsWarning() {
            MissionPatchingReport subject = new MissionPatchingReport("Test Title", "Test details, like what went wrong, what needs to be done to fix it");

            subject.title.Should().Be("Warning: Test Title");
            subject.detail.Should().Be("Test details, like what went wrong, what needs to be done to fix it");
            subject.error.Should().BeFalse();
        }

        [Fact]
        public void ShouldSetFieldsFromException() {
            MissionPatchingReport subject = new MissionPatchingReport(new Exception("An error occured"));

            subject.title.Should().Be("An error occured");
            subject.detail.Should().Be("System.Exception: An error occured");
            subject.error.Should().BeTrue();
        }
    }
}
