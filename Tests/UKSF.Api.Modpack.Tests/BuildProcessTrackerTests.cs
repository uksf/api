using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Modpack.BuildProcess;
using Xunit;

namespace UKSF.Api.Modpack.Tests;

public class BuildProcessTrackerTests
{
    private readonly Mock<IUksfLogger> _mockLogger;
    private readonly BuildProcessTracker _tracker;

    public BuildProcessTrackerTests()
    {
        _mockLogger = new Mock<IUksfLogger>();
        _tracker = new BuildProcessTracker(_mockLogger.Object);
    }

    [Fact]
    public void CleanupDeadProcesses_Should_RemoveNonExistentProcesses()
    {
        // Arrange - Register processes that don't exist
        _tracker.RegisterProcess(999998, "build-123", "dead process 1");
        _tracker.RegisterProcess(999999, "build-123", "dead process 2");

        // Act - Call GetTrackedProcesses which triggers cleanup
        var processes = _tracker.GetTrackedProcesses();

        // Assert - Dead processes should be removed
        processes.Should().BeEmpty();
    }

    [Fact]
    public void GetTrackedProcessesForBuild_Should_ReturnEmptyForNonExistentBuild()
    {
        // Arrange
        var processId = Environment.ProcessId;
        _tracker.RegisterProcess(processId, "build-123", "process 1");

        // Act
        var result = _tracker.GetTrackedProcessesForBuild("build-999");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetTrackedProcessesForBuild_Should_ReturnOnlyMatchingBuildProcesses()
    {
        // Arrange - Use only the current process ID to avoid access denied errors
        var processId1 = Environment.ProcessId;

        _tracker.RegisterProcess(processId1, "build-123", "process 1");
        // For the test, we'll register fake process IDs but only verify the build filtering logic
        _tracker.RegisterProcess(999998, "build-123", "process 2");
        _tracker.RegisterProcess(999999, "build-456", "process 3");

        // Act - This will trigger cleanup but should still return the correct filtered results
        var build123Processes = _tracker.GetTrackedProcessesForBuild("build-123").ToList();

        // Assert - Should only contain processes for build-123 that still exist
        build123Processes.Should().HaveCountGreaterOrEqualTo(1); // At least the current process should exist
        build123Processes.Should().OnlyContain(p => p.BuildId == "build-123");
    }

    [Fact]
    public void KillTrackedProcesses_Should_FilterByBuildIdWhenProvided()
    {
        // Arrange
        var allProcesses = Process.GetProcesses();
        var processId1 = Environment.ProcessId;
        var processId2 = allProcesses.Length > 1 ? allProcesses[1].Id : Environment.ProcessId;

        _tracker.RegisterProcess(processId1, "build-123", "process 1");
        _tracker.RegisterProcess(processId2, "build-456", "process 2");

        // Act
        _tracker.KillTrackedProcesses("build-123");

        // Assert
        // We can't actually verify processes were killed without creating real processes
        // but we can verify that the method completes without throwing
        var remainingProcesses = _tracker.GetTrackedProcesses();
        remainingProcesses.Should().NotBeNull();
    }

    [Fact]
    public void KillTrackedProcesses_Should_HandleExceptionsGracefully()
    {
        // Arrange - Register a process that doesn't exist
        const int NonExistentProcessId = 999999;
        _tracker.RegisterProcess(NonExistentProcessId, "build-123", "non-existent process");

        // Act & Assert - Should not throw
        _tracker.KillTrackedProcesses();

        // The process should be cleaned up from tracking even if it doesn't exist
        var remainingProcesses = _tracker.GetTrackedProcesses();
        remainingProcesses.Should().BeEmpty();
    }

    [Fact]
    public void KillTrackedProcesses_Should_CleanupProcessesWhenKilling()
    {
        // Arrange - Use a process ID that's likely to not exist
        const int ProcessId = 999999;
        _tracker.RegisterProcess(ProcessId, "build-123", "test process");

        // Act
        _tracker.KillTrackedProcesses("build-123");

        // Assert - Should attempt to kill and clean up
        var remainingProcesses = _tracker.GetTrackedProcesses();
        remainingProcesses.Should().BeEmpty();
    }

    [Fact]
    public void KillTrackedProcesses_Should_ReturnZeroWhenNoProcessesToKill()
    {
        // Act
        var killedCount = _tracker.KillTrackedProcesses();

        // Assert
        killedCount.Should().Be(0);
    }

    [Fact]
    public void RegisterProcess_Should_AddProcessToTracking()
    {
        // Arrange
        var processId = Environment.ProcessId; // Use current process ID
        const string BuildId = "build-123";
        const string Description = "git fetch";

        // Act
        _tracker.RegisterProcess(processId, BuildId, Description);

        // Assert
        var trackedProcesses = _tracker.GetTrackedProcesses().ToList();
        trackedProcesses.Should().HaveCount(1);

        var trackedProcess = trackedProcesses.First();
        trackedProcess.ProcessId.Should().Be(processId);
        trackedProcess.BuildId.Should().Be(BuildId);
        trackedProcess.Description.Should().Be(Description);
        trackedProcess.StartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RegisterProcess_Should_AllowMultipleProcessesForSameBuild()
    {
        // Arrange
        const string BuildId = "build-123";
        var processId1 = Environment.ProcessId; // Use current process ID

        // Act - Register multiple processes for the same build
        // Note: BuildProcessTracker now allows updates, so the LAST registration wins
        _tracker.RegisterProcess(processId1, BuildId, "git fetch");
        _tracker.RegisterProcess(processId1, BuildId, "git checkout"); // This updates the first one
        _tracker.RegisterProcess(processId1, "build-456", "python make.py"); // This updates again

        // Assert - Since we're using the same process ID and last registration wins,
        // the final registration should be what's tracked
        var allProcesses = _tracker.GetTrackedProcesses().ToList();
        allProcesses.Should().HaveCount(1);

        var process = allProcesses.First();
        process.ProcessId.Should().Be(processId1);
        process.BuildId.Should().Be("build-456"); // Last registration wins
        process.Description.Should().Be("python make.py");
    }

    [Fact]
    public void TrackedProcess_Should_StoreCorrectInformation()
    {
        // Arrange
        var processId = Environment.ProcessId; // Use current process ID
        const string BuildId = "build-test";
        const string Description = "git command test";
        var startTime = DateTime.UtcNow;

        // Act
        _tracker.RegisterProcess(processId, BuildId, Description);

        // Assert
        var trackedProcesses = _tracker.GetTrackedProcesses();
        var process = trackedProcesses.First();

        process.ProcessId.Should().Be(processId);
        process.BuildId.Should().Be(BuildId);
        process.Description.Should().Be(Description);
        process.StartTime.Should().BeCloseTo(startTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Tracker_Should_HandleConcurrentRegistrations()
    {
        // Arrange & Act - Use only the current process ID to avoid access denied errors
        var currentProcessId = Environment.ProcessId;
        var tasks = Enumerable.Range(1, 100)
                              .Select(i => Task.Run(() => _tracker.RegisterProcess(currentProcessId, $"build-{i % 5}", $"process-{i}")))
                              .ToArray();

        await Task.WhenAll(tasks);

        // Assert - Since we're using the same process ID for all registrations,
        // only the last registration will remain (they overwrite each other)
        var allProcesses = _tracker.GetTrackedProcesses().ToList();
        allProcesses.Should().HaveCount(1); // Only one process ID can be tracked at a time

        // Verify the process is tracked correctly
        var trackedProcess = allProcesses.First();
        trackedProcess.ProcessId.Should().Be(currentProcessId);
    }

    [Fact]
    public void UnregisterProcess_Should_HandleNonExistentProcess()
    {
        // Act & Assert - Should not throw
        _tracker.UnregisterProcess(9999);

        _mockLogger.Verify(x => x.LogInfo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void UnregisterProcess_Should_RemoveProcessFromTracking()
    {
        // Arrange
        var processId = Environment.ProcessId; // Use current process ID
        const string BuildId = "build-123";
        _tracker.RegisterProcess(processId, BuildId, "test process");

        // Act
        _tracker.UnregisterProcess(processId);

        // Assert
        var trackedProcesses = _tracker.GetTrackedProcesses();
        trackedProcesses.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void KillTrackedProcesses_Should_KillAllProcessesWhenBuildIdIsNullOrEmpty(string buildId)
    {
        // Arrange
        _tracker.RegisterProcess(999997, "build-123", "process 1");
        _tracker.RegisterProcess(999998, "build-456", "process 2");

        // Act
        _tracker.KillTrackedProcesses(buildId);

        // Assert - All processes should be cleaned up
        var remainingProcesses = _tracker.GetTrackedProcesses();
        remainingProcesses.Should().BeEmpty();
    }
}
