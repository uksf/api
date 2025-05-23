using System;
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
    private readonly Mock<IStepLogger> _mockStepLogger = new();
    private readonly Mock<IUksfLogger> _mockUksfLogger = new();

    [Fact]
    public void Run_Should_ThrowException_WithCorrectExitCode_When_ProcessExitsWithNonZeroCode()
    {
        // Arrange
        var buildProcessHelper = new BuildProcessHelper(
            _mockStepLogger.Object,
            _mockUksfLogger.Object,
            _cancellationTokenSource,
            true // Ensure errors are raised for this test
        );

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

        var workingDirectory = "."; // Current directory
        var timeout = 5000; // 5 seconds

        // Act & Assert
        var exception = Assert.Throws<Exception>(() => buildProcessHelper.Run(workingDirectory, executable, args, timeout));

        exception.Message.Should().Contain("Process failed with exit code 42");

        // Verify logs if necessary, for example, that an error was logged
        // _mockUksfLogger.Verify(logger => logger.LogError(It.Is<string>(s => s.Contains("Build process exit code was non-zero (42)"))), Times.AtLeastOnce());
    }
}
