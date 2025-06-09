using FluentAssertions;
using System.Collections.Generic;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Core.Models;
using Xunit;

namespace UKSF.Api.ArmaMissions.Tests.Models;

public class MissionPatchingResultTests
{
    [Fact]
    public void ShouldInitializeWithDefaultValues()
    {
        // Act
        var subject = new MissionPatchingResult();

        // Assert
        subject.PlayerCount.Should().Be(0);
        subject.Success.Should().BeFalse();
        subject.Reports.Should().NotBeNull();
        subject.Reports.Should().BeEmpty();
    }

    [Fact]
    public void ShouldAllowSettingPlayerCount()
    {
        // Arrange
        var subject = new MissionPatchingResult();
        const int expectedPlayerCount = 15;

        // Act
        subject.PlayerCount = expectedPlayerCount;

        // Assert
        subject.PlayerCount.Should().Be(expectedPlayerCount);
    }

    [Fact]
    public void ShouldAllowSettingSuccess()
    {
        // Arrange
        var subject = new MissionPatchingResult();

        // Act
        subject.Success = true;

        // Assert
        subject.Success.Should().BeTrue();
    }

    [Fact]
    public void Reports_ShouldUseModernCollectionInitialization()
    {
        // Arrange & Act
        var subject = new MissionPatchingResult();

        // Assert
        subject.Reports.Should().NotBeNull();
        subject.Reports.Should().BeOfType<List<ValidationReport>>();
    }

    [Fact]
    public void ShouldAllowAddingReports()
    {
        // Arrange
        var subject = new MissionPatchingResult();
        var report1 = new ValidationReport("Test Warning", "Warning details");
        var report2 = new ValidationReport("Test Error", "Error details", true);

        // Act
        subject.Reports.Add(report1);
        subject.Reports.Add(report2);

        // Assert
        subject.Reports.Should().HaveCount(2);
        subject.Reports[0].Should().Be(report1);
        subject.Reports[1].Should().Be(report2);
        subject.Reports[0].Error.Should().BeFalse();
        subject.Reports[1].Error.Should().BeTrue();
    }

    [Fact]
    public void ShouldAllowReplacingReportsCollection()
    {
        // Arrange
        var subject = new MissionPatchingResult();
        var initialReport = new ValidationReport("Initial", "Initial report");
        subject.Reports.Add(initialReport);

        var newReports = new List<ValidationReport> { new("New Report 1", "Details 1"), new("New Report 2", "Details 2") };

        // Act
        subject.Reports = newReports;

        // Assert
        subject.Reports.Should().BeSameAs(newReports);
        subject.Reports.Should().HaveCount(2);
        subject.Reports[0].Title.Should().Be("Warning: New Report 1");
        subject.Reports[1].Title.Should().Be("Warning: New Report 2");
    }

    [Fact]
    public void ShouldAllowSettingAllPropertiesTogether()
    {
        // Arrange
        var subject = new MissionPatchingResult();
        var reports = new List<ValidationReport> { new("Success Report", "Mission patched successfully") };

        // Act
        subject.PlayerCount = 20;
        subject.Success = true;
        subject.Reports = reports;

        // Assert
        subject.PlayerCount.Should().Be(20);
        subject.Success.Should().BeTrue();
        subject.Reports.Should().BeSameAs(reports);
        subject.Reports.Should().HaveCount(1);
    }

    [Fact]
    public void ShouldAllowNegativePlayerCount()
    {
        // Arrange
        var subject = new MissionPatchingResult();

        // Act
        subject.PlayerCount = -1;

        // Assert
        subject.PlayerCount.Should().Be(-1);
    }

    [Fact]
    public void ShouldAllowEmptyReports()
    {
        // Arrange
        var subject = new MissionPatchingResult();
        subject.Reports.Add(new ValidationReport("Test", "Test"));

        // Act
        subject.Reports.Clear();

        // Assert
        subject.Reports.Should().BeEmpty();
        subject.Reports.Should().HaveCount(0);
    }
}
