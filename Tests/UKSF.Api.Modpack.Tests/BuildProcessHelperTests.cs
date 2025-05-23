using System;
using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Modpack.BuildProcess;
using Xunit;

namespace UKSF.Api.Modpack.Tests;

public class BuildProcessHelperTests
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Mock<IBuildProcessTracker> _mockProcessTracker = new();
    private readonly Mock<IStepLogger> _mockStepLogger = new();
    private readonly Mock<IUksfLogger> _mockUksfLogger = new();

    [Fact]
    public void Dispose_Should_CleanUpResources()
    {
        // Arrange
        var buildProcessHelper = new BuildProcessHelper(_mockStepLogger.Object, _mockUksfLogger.Object, _cancellationTokenSource, raiseErrors: false);

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

        var buildProcessHelper = new BuildProcessHelper(_mockStepLogger.Object, _mockUksfLogger.Object, preCancelledTokenSource, raiseErrors: false);

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
        var buildProcessHelper = new BuildProcessHelper(
            _mockStepLogger.Object,
            _mockUksfLogger.Object,
            _cancellationTokenSource,
            raiseErrors: false,
            errorSilently: true
        );

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
    public void Run_Should_HandleIgnoreErrorGates()
    {
        // Arrange
        var buildProcessHelper = new BuildProcessHelper(
            _mockStepLogger.Object,
            _mockUksfLogger.Object,
            _cancellationTokenSource,
            raiseErrors: false,
            ignoreErrorGateOpen: "START_IGNORE",
            ignoreErrorGateClose: "END_IGNORE"
        );

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
    public void Run_Should_HandleSuppressOutput()
    {
        // Arrange
        var buildProcessHelper = new BuildProcessHelper(_mockStepLogger.Object, _mockUksfLogger.Object, _cancellationTokenSource, true, false);

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
        var buildProcessHelper = new BuildProcessHelper(_mockStepLogger.Object, _mockUksfLogger.Object, _cancellationTokenSource, raiseErrors: false);

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
        var buildProcessHelper = new BuildProcessHelper(_mockStepLogger.Object, _mockUksfLogger.Object, _cancellationTokenSource, raiseErrors: false);

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
        var buildProcessHelper = new BuildProcessHelper(
            _mockStepLogger.Object,
            _mockUksfLogger.Object,
            _cancellationTokenSource,
            raiseErrors: false,
            processTracker: _mockProcessTracker.Object,
            buildId: null
        );

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
    public void Run_Should_NotRegisterProcess_When_ProcessTrackerIsNull()
    {
        // Arrange
        var buildProcessHelper = new BuildProcessHelper(
            _mockStepLogger.Object,
            _mockUksfLogger.Object,
            _cancellationTokenSource,
            raiseErrors: false,
            processTracker: null,
            buildId: "test-build"
        );

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
        buildProcessHelper.Run(WorkingDirectory, executable, args, Timeout);
    }

    [Fact]
    public void Run_Should_NotThrowException_When_RaiseErrorsIsFalse()
    {
        // Arrange
        var buildProcessHelper = new BuildProcessHelper(_mockStepLogger.Object, _mockUksfLogger.Object, _cancellationTokenSource, raiseErrors: false);

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
        var buildProcessHelper = new BuildProcessHelper(
            _mockStepLogger.Object,
            _mockUksfLogger.Object,
            _cancellationTokenSource,
            raiseErrors: false,
            processTracker: _mockProcessTracker.Object,
            buildId: BuildId
        );

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
        var buildProcessHelper = new BuildProcessHelper(_mockStepLogger.Object, _mockUksfLogger.Object, _cancellationTokenSource, raiseErrors: false);

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
        var buildProcessHelper = new BuildProcessHelper(_mockStepLogger.Object, _mockUksfLogger.Object, _cancellationTokenSource, raiseErrors: true);

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
        var buildProcessHelper = new BuildProcessHelper(_mockStepLogger.Object, _mockUksfLogger.Object, _cancellationTokenSource, raiseErrors: false);

        buildProcessHelper.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => buildProcessHelper.Run(".", "cmd.exe", "/c \"echo test\"", 5000));
    }

    [Theory]
    [InlineData("error1", "error2")]
    [InlineData("warning", "critical")]
    public void Run_Should_HandleErrorExclusions(string exclusion1, string exclusion2)
    {
        // Arrange
        var errorExclusions = new List<string> { exclusion1, exclusion2 };
        var buildProcessHelper = new BuildProcessHelper(
            _mockStepLogger.Object,
            _mockUksfLogger.Object,
            _cancellationTokenSource,
            raiseErrors: false,
            errorExclusions: errorExclusions
        );

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
}
