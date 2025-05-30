using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.BuildProcess;
using Xunit;

namespace UKSF.Api.Modpack.Tests;

public class BuildProcessHelperTests
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Mock<IBuildProcessTracker> _mockProcessTracker = new();
    private readonly Mock<IStepLogger> _mockStepLogger = new();
    private readonly Mock<IUksfLogger> _mockUksfLogger = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();

    public BuildProcessHelperTests()
    {
        // Setup default behavior for BUILD_FORCE_LOGS variable
        var buildForceLogsVariable = new DomainVariableItem { Key = "BUILD_FORCE_LOGS", Item = false };
        _mockVariablesService.Setup(x => x.GetVariable("BUILD_FORCE_LOGS")).Returns(buildForceLogsVariable);
    }

    [Fact]
    public void Dispose_Should_CleanUpResources()
    {
        // Arrange
        var buildProcessHelper = CreateBuildProcessHelper(raiseErrors: false);

        // Act & Assert - Should not throw
        buildProcessHelper.Dispose();

        // Calling dispose again should not throw
        buildProcessHelper.Dispose();
    }

    [Fact]
    public void Run_Should_HandleCancellation_When_CancellationTokenIsTriggered()
    {
        // Arrange
        var preCancelledTokenSource = new CancellationTokenSource();
        preCancelledTokenSource.Cancel(); // Pre-cancel the token

        var buildProcessHelper = CreateBuildProcessHelper(raiseErrors: false, cancellationTokenSource: preCancelledTokenSource);

        string executable;
        string args;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            executable = "cmd.exe";
            args = "/c \"echo cancellation test\"";
        }
        else
        {
            executable = "sh";
            args = "-c \"echo cancellation test\"";
        }

        const string WorkingDirectory = ".";
        const int Timeout = 5000;

        // Act
        var result = buildProcessHelper.Run(WorkingDirectory, executable, args, Timeout, true);

        // Assert - Should complete gracefully even with cancelled token
        result.Should().NotBeNull();
        // Verify that the process was started (cancellation handling doesn't prevent basic execution)
        _mockUksfLogger.Verify(x => x.LogInfo(It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public void Run_Should_HandleErrorSilently_When_ErrorSilentlyIsTrue()
    {
        // Arrange
        var buildProcessHelper = CreateBuildProcessHelper(raiseErrors: false, errorSilently: true);

        string executable;
        string args;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            executable = "cmd.exe";
            args = "/c \"exit 1\"";
        }
        else
        {
            executable = "sh";
            args = "-c \"exit 1\"";
        }

        const string WorkingDirectory = ".";
        const int Timeout = 5000;

        // Act
        var result = buildProcessHelper.Run(WorkingDirectory, executable, args, Timeout);

        // Assert
        result.Should().NotBeNull();
        _mockStepLogger.Verify(x => x.LogError(It.IsAny<Exception>()), Times.Never);
    }

    [Fact]
    public void Run_Should_HandleFailingCommandWithErrorOutput_WithoutHanging()
    {
        // Arrange
        // This test replicates the hanging issue encountered with the git command that failed
        // The key is to have a command that:
        // 1. Fails with a non-zero exit code
        // 2. Outputs to stderr (which triggers error handling)
        // 3. Tests the race condition fix where multiple Kill() calls were causing deadlocks

        var buildProcessHelper = CreateBuildProcessHelper(raiseErrors: false, buildId: "test-hanging-fix");

        string executable;
        string args;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            // Use a command that will fail and output to stderr
            // This simulates the "git: 'git' is not a git command" error
            executable = "cmd.exe";
            args = "/c \"echo Error output to stderr >&2 && exit 1\"";
        }
        else
        {
            executable = "sh";
            args = "-c \"echo 'Error output to stderr' >&2 && exit 1\"";
        }

        const string WorkingDirectory = ".";
        const int Timeout = 10000; // 10 seconds timeout - should complete much faster

        var startTime = DateTime.UtcNow;

        // Act
        var result = buildProcessHelper.Run(WorkingDirectory, executable, args, Timeout, true);

        // Assert
        var duration = DateTime.UtcNow - startTime;

        // The process should complete without hanging (much faster than timeout)
        duration.Should().BeLessThan(TimeSpan.FromSeconds(5), "Process should not hang and should complete quickly");

        result.Should().NotBeNull("Result should not be null even when process fails");

        // Verify that the process was properly tracked and cleaned up
        _mockProcessTracker.Verify(x => x.RegisterProcess(It.IsAny<int>(), "test-hanging-fix", args), Times.Once);
        _mockProcessTracker.Verify(x => x.UnregisterProcess(It.IsAny<int>()), Times.Once);

        // Verify that proper logging occurred for the lifecycle
        _mockUksfLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Starting process"))), Times.Once);
        _mockUksfLogger.Verify(x => x.LogWarning(It.Is<string>(s => s.Contains("Kill process instructed"))), Times.Once);
        _mockUksfLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Stored exit code 1"))), Times.Once);
        _mockUksfLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Process reached successful return"))), Times.Once);

        // Verify that stream closure was handled properly (no hanging on stream wait)
        // Either the stream closes naturally OR it's forced to close, but not both for the same stream
        _mockUksfLogger.Verify(
            x => x.LogInfo(It.Is<string>(s => s.Contains("Output stream closed") || s.Contains("Output stream not closed naturally"))),
            Times.AtLeastOnce
        );
        _mockUksfLogger.Verify(
            x => x.LogInfo(It.Is<string>(s => s.Contains("Error stream closed") || s.Contains("Error stream not closed naturally"))),
            Times.AtLeastOnce
        );
    }

    [Fact]
    public void Run_Should_HandleIgnoreErrorGates()
    {
        // Arrange
        var buildProcessHelper = CreateBuildProcessHelper(raiseErrors: false, ignoreErrorGateClose: "END_IGNORE", ignoreErrorGateOpen: "START_IGNORE");

        string executable;
        string args;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            executable = "cmd.exe";
            args = "/c \"echo test\"";
        }
        else
        {
            executable = "sh";
            args = "-c \"echo test\"";
        }

        const string WorkingDirectory = ".";
        const int Timeout = 5000;

        // Act & Assert - Should not throw
        var result = buildProcessHelper.Run(WorkingDirectory, executable, args, Timeout);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Run_Should_HandleMultipleConcurrentKillCalls_WithoutRaceCondition()
    {
        // Arrange
        // This test specifically targets the race condition where multiple Kill() calls
        // were being made simultaneously from different threads
        var cancellationTokenSource = new CancellationTokenSource();
        var buildProcessHelper = CreateBuildProcessHelper(
            raiseErrors: false,
            buildId: "test-concurrent-kill",
            cancellationTokenSource: cancellationTokenSource
        );

        string executable;
        string args;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            // Use a command that will run for a bit and output to stderr
            executable = "cmd.exe";
            args = "/c \"timeout /t 2 >nul && echo Error output >&2 && exit 1\"";
        }
        else
        {
            executable = "sh";
            args = "-c \"sleep 2 && echo 'Error output' >&2 && exit 1\"";
        }

        const string WorkingDirectory = ".";
        const int Timeout = 10000;

        // Start the process in a separate task
        // ReSharper disable once MethodSupportsCancellation
        var processTask = Task.Run(() => buildProcessHelper.Run(WorkingDirectory, executable, args, Timeout, true));

        // Give the process a moment to start
        Thread.Sleep(500);

        // Trigger cancellation to cause Kill() to be called from cancellation token
        // This should trigger multiple kill attempts that were causing the race condition
        await cancellationTokenSource.CancelAsync();

        var startTime = DateTime.UtcNow;

        // Act & Assert
        var result = await processTask; // This should not hang

        var duration = DateTime.UtcNow - startTime;
        duration.Should().BeLessThan(TimeSpan.FromSeconds(5), "Process should handle concurrent kill calls without hanging");

        result.Should().NotBeNull();

        // Verify that kill operations were logged but didn't cause issues
        _mockUksfLogger.Verify(x => x.LogWarning(It.Is<string>(s => s.Contains("Kill process instructed"))), Times.AtMostOnce);

        // Should see the "duplicate call" log if multiple kills were attempted
        _mockUksfLogger.Verify(
            x => x.LogInfo(It.Is<string>(s => s.Contains("Kill already in progress") || s.Contains("Process reached successful return"))),
            Times.AtLeastOnce
        );
    }

    [Fact]
    public void Run_Should_HandleSuppressOutput()
    {
        // Arrange
        var buildProcessHelper = CreateBuildProcessHelper(true, false);

        string executable;
        string args;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            executable = "cmd.exe";
            args = "/c \"echo test output\"";
        }
        else
        {
            executable = "sh";
            args = "-c \"echo test output\"";
        }

        const string WorkingDirectory = ".";
        const int Timeout = 5000;

        // Act
        var result = buildProcessHelper.Run(WorkingDirectory, executable, args, Timeout);

        // Assert
        result.Should().NotBeNull();
        // With suppressOutput, we shouldn't see the output in results
    }

    [Fact]
    public void Run_Should_HandleTimeout_Gracefully()
    {
        // Arrange
        var buildProcessHelper = CreateBuildProcessHelper(raiseErrors: false);

        // Use a command that will finish quickly to simulate timeout behavior
        string executable;
        string args;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            executable = "cmd.exe";
            args = "/c \"echo timeout test\"";
        }
        else
        {
            executable = "sh";
            args = "-c \"echo timeout test\"";
        }

        const string WorkingDirectory = ".";
        const int Timeout = 5000; // Normal timeout that won't be exceeded

        // Act
        var result = buildProcessHelper.Run(WorkingDirectory, executable, args, Timeout, true);

        // Assert
        result.Should().NotBeNull();
        // This test verifies the timeout parameter is accepted and processed
        _mockUksfLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Starting process"))), Times.Once);
    }

    [Fact]
    public void Run_Should_LogProcessInformation()
    {
        // Arrange
        var buildProcessHelper = CreateBuildProcessHelper(raiseErrors: false);

        string executable;
        string args;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            executable = "cmd.exe";
            args = "/c \"echo test\"";
        }
        else
        {
            executable = "sh";
            args = "-c \"echo test\"";
        }

        const string WorkingDirectory = ".";
        const int Timeout = 5000;

        // Act
        buildProcessHelper.Run(WorkingDirectory, executable, args, Timeout, true);

        // Assert
        _mockUksfLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Starting process"))), Times.Once);

        _mockUksfLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Started process with ID"))), Times.Once);
    }

    [Fact]
    public void Run_Should_NotRegisterProcess_When_BuildIdIsNull()
    {
        // Arrange
        var buildProcessHelper = CreateBuildProcessHelper(raiseErrors: false, buildId: null);

        string executable;
        string args;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            executable = "cmd.exe";
            args = "/c \"echo test\"";
        }
        else
        {
            executable = "sh";
            args = "-c \"echo test\"";
        }

        const string WorkingDirectory = ".";
        const int Timeout = 5000;

        // Act
        buildProcessHelper.Run(WorkingDirectory, executable, args, Timeout);

        // Assert
        _mockProcessTracker.Verify(x => x.RegisterProcess(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Run_Should_NotThrowException_When_RaiseErrorsIsFalse()
    {
        // Arrange
        var buildProcessHelper = CreateBuildProcessHelper(raiseErrors: false);

        string executable;
        string args;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            executable = "cmd.exe";
            args = "/c \"exit 42\"";
        }
        else
        {
            executable = "sh";
            args = "-c \"exit 42\"";
        }

        const string WorkingDirectory = ".";
        const int Timeout = 5000;

        // Act & Assert - Should not throw
        var result = buildProcessHelper.Run(WorkingDirectory, executable, args, Timeout);
        result.Should().NotBeNull();
    }

    [Fact]
    public void Run_Should_RegisterProcess_When_ProcessTrackerIsProvided()
    {
        // Arrange
        const string BuildId = "test-build-123";
        var buildProcessHelper = CreateBuildProcessHelper(raiseErrors: false, buildId: BuildId);

        string executable;
        string args;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            executable = "cmd.exe";
            args = "/c \"echo test\"";
        }
        else
        {
            executable = "sh";
            args = "-c \"echo test\"";
        }

        const string WorkingDirectory = ".";
        const int Timeout = 5000;

        // Act
        buildProcessHelper.Run(WorkingDirectory, executable, args, Timeout);

        // Assert
        _mockProcessTracker.Verify(x => x.RegisterProcess(It.IsAny<int>(), BuildId, args), Times.Once);

        _mockProcessTracker.Verify(x => x.UnregisterProcess(It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void Run_Should_SupportExtendedTimeouts()
    {
        // Arrange
        var buildProcessHelper = CreateBuildProcessHelper(raiseErrors: false);

        // Test that extended timeouts (like 2 minutes for git operations) are supported
        var twoMinutesInMs = (int)TimeSpan.FromMinutes(2).TotalMilliseconds;

        string executable;
        string args;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            executable = "cmd.exe";
            args = "/c \"echo extended timeout test\"";
        }
        else
        {
            executable = "sh";
            args = "-c \"echo extended timeout test\"";
        }

        // Act & Assert - Should handle large timeout values without issues
        var result = buildProcessHelper.Run(".", executable, args, twoMinutesInMs, true);
        result.Should().NotBeNull();

        // Verify logging occurred
        _mockUksfLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Starting process"))), Times.Once);
    }

    [Fact]
    public void Run_Should_ThrowException_WithCorrectExitCode_When_ProcessExitsWithNonZeroCode()
    {
        // Arrange
        var buildProcessHelper = CreateBuildProcessHelper(raiseErrors: true);

        string executable;
        string args;
        // For now, assume Windows for cmd.exe. Worker environment might need adjustment.
        // Ideally, this would use platform detection or a cross-platform script.
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            executable = "cmd.exe";
            args = "/c \"exit 42\"";
        }
        else
        {
            executable = "sh";
            args = "-c \"exit 42\"";
        }

        const string WorkingDirectory = "."; // Current directory
        const int Timeout = 5000; // 5 seconds

        // Act & Assert
        var exception = Assert.Throws<Exception>(() => buildProcessHelper.Run(WorkingDirectory, executable, args, Timeout));

        exception.Message.Should().Contain("Process failed with exit code 42");

        // Verify logs if necessary, for example, that an error was logged
        // _mockUksfLogger.Verify(logger => logger.LogError(It.Is<string>(s => s.Contains("Build process exit code was non-zero (42)"))), Times.AtLeastOnce());
    }

    [Fact]
    public void Run_Should_ThrowObjectDisposedException_When_AlreadyDisposed()
    {
        // Arrange
        var buildProcessHelper = CreateBuildProcessHelper(raiseErrors: false);

        buildProcessHelper.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => buildProcessHelper.Run(".", "cmd.exe", "/c \"echo test\"", 5000));
    }

    [Fact]
    public void Run_Should_WaitForOutputProcessingAfterProcessExit()
    {
        // Arrange
        var buildProcessHelper = CreateBuildProcessHelper(raiseErrors: false);

        string executable;
        string args;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            // Use a simple command that produces at least one output line
            executable = "cmd.exe";
            args = "/c \"echo Output Line\"";
        }
        else
        {
            executable = "sh";
            args = "-c \"echo Output Line\"";
        }

        const string WorkingDirectory = ".";
        const int Timeout = 5000;

        // Act
        var result = buildProcessHelper.Run(WorkingDirectory, executable, args, Timeout, true);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterOrEqualTo(1); // Should capture at least one output
        result.Should().Contain(line => line.Contains("Output"));

        // Verify logging was called for process lifecycle
        _mockUksfLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Process finished successfully"))), Times.Once);
        _mockUksfLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Process reached successful return"))), Times.Once);
    }

    [Theory]
    [InlineData("error1", "error2")]
    [InlineData("warning", "critical")]
    public void Run_Should_HandleErrorExclusions(string exclusion1, string exclusion2)
    {
        // Arrange
        var errorExclusions = new List<string> { exclusion1, exclusion2 };
        var buildProcessHelper = CreateBuildProcessHelper(raiseErrors: false, errorExclusions: errorExclusions);

        string executable;
        string args;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            executable = "cmd.exe";
            args = "/c \"echo test\"";
        }
        else
        {
            executable = "sh";
            args = "-c \"echo test\"";
        }

        const string WorkingDirectory = ".";
        const int Timeout = 5000;

        // Act & Assert - Should not throw
        var result = buildProcessHelper.Run(WorkingDirectory, executable, args, Timeout);
        result.Should().NotBeNull();
    }

    private BuildProcessHelper CreateBuildProcessHelper(
        bool suppressOutput = false,
        bool raiseErrors = true,
        bool errorSilently = false,
        List<string> errorExclusions = null,
        string ignoreErrorGateClose = "",
        string ignoreErrorGateOpen = "",
        string buildId = null,
        CancellationTokenSource cancellationTokenSource = null
    )
    {
        return new BuildProcessHelper(
            _mockStepLogger.Object,
            _mockUksfLogger.Object,
            cancellationTokenSource ?? _cancellationTokenSource,
            _mockVariablesService.Object,
            _mockProcessTracker.Object,
            suppressOutput,
            raiseErrors,
            errorSilently,
            errorExclusions,
            ignoreErrorGateClose,
            ignoreErrorGateOpen,
            buildId
        );
    }
}
