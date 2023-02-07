using FluentAssertions;
using UKSF.Api.Core.Models;
using Xunit;

namespace UKSF.Tests.Unit.Models.Mission;

public class MissionPatchingReportTests
{
    [Fact]
    public void ShouldSetFieldsAsError()
    {
        ValidationReport subject = new("Test Title", "Test details, like what went wrong, what needs to be done to fix it", true);

        subject.Title.Should().Be("Error: Test Title");
        subject.Detail.Should().Be("Test details, like what went wrong, what needs to be done to fix it");
        subject.Error.Should().BeTrue();
    }

    [Fact]
    public void ShouldSetFieldsAsWarning()
    {
        ValidationReport subject = new("Test Title", "Test details, like what went wrong, what needs to be done to fix it");

        subject.Title.Should().Be("Warning: Test Title");
        subject.Detail.Should().Be("Test details, like what went wrong, what needs to be done to fix it");
        subject.Error.Should().BeFalse();
    }

    [Fact]
    public void ShouldSetFieldsFromException()
    {
        ValidationReport subject = new(new("An error occured"));

        subject.Title.Should().Be("An error occured");
        subject.Detail.Should().Be("System.Exception: An error occured");
        subject.Error.Should().BeTrue();
    }
}
