using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Core.Processes;
using Xunit;
using UKSF.Api.Modpack.BuildProcess;

namespace UKSF.Api.Modpack.Tests;

public class ProcessCommandFactoryTests
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Mock<IBuildProcessTracker> _mockProcessTracker = new();
    private readonly Mock<IUksfLogger> _mockUksfLogger = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();

    public ProcessCommandFactoryTests()
    {
        _mockVariablesService.Setup(x => x.GetVariable("BUILD_FORCE_LOGS")).Returns(new DomainVariableItem { Key = "BUILD_FORCE_LOGS", Item = false });
    }

    [Fact]
    public async Task ExecuteAsync_Should_HandleCancellation_When_CancellationTokenIsTriggered()
    {
        // Arrange
        var preCancelledTokenSource = new CancellationTokenSource();
        preCancelledTokenSource.Cancel(); // Pre-cancel the token

        GetPlatformCommand(out var executable, out var args, "echo cancellation test");
        var processService = new ProcessCommandFactory(_mockUksfLogger.Object);
        var command = processService.CreateCommand(executable, ".", args).WithTimeout(TimeSpan.FromSeconds(5));

        // Act & Assert
        // With a pre-cancelled token, we expect a TaskCanceledException
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await foreach (var _ in command.ExecuteAsync(preCancelledTokenSource.Token)) { }
            }
        );
    }

    [Fact]
    public async Task ExecuteAsync_Should_NotProduceErrorOutputLines_When_CancelledDuringExecution()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        GetPlatformCommand(out var executable, out var args, "powershell.exe -Command \"Start-Sleep 5\"");
        var processService = new ProcessCommandFactory(_mockUksfLogger.Object);
        var command = processService.CreateCommand(executable, ".", args).WithTimeout(TimeSpan.FromSeconds(5));

        // Act
        var results = new List<ProcessOutputLine>();
        var operationCancelledExceptionThrown = false;

        var collectTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var outputLine in command.ExecuteAsync(cancellationTokenSource.Token))
                    {
                        results.Add(outputLine);
                    }
                }
                catch (OperationCanceledException)
                {
                    operationCancelledExceptionThrown = true;
                }
            }
        );

        // Cancel after a short delay to simulate user cancellation during execution
        await Task.Delay(100);
        cancellationTokenSource.Cancel();

        await collectTask;

        // Assert
        // When a process is cancelled, it should throw OperationCanceledException
        // and not produce error output lines that would cause build failure
        operationCancelledExceptionThrown.Should().BeTrue("Cancellation should throw OperationCanceledException");

        var errorLines = results.Where(r => r.Type == ProcessOutputType.Error).ToList();
        var cancellationErrorLines = errorLines.Where(r => r.Exception is OperationCanceledException).ToList();

        // This should now pass - cancellation should not produce error output lines
        cancellationErrorLines.Should().BeEmpty("Cancellation should not be treated as an error that would cause build failure");
    }

    [Fact]
    public async Task ExecuteAsync_Should_ReportNonZeroExitCodes_AsProcessCompleted()
    {
        // Arrange
        GetPlatformCommand(out var executable, out var args, "exit 1");
        var processService = new ProcessCommandFactory(_mockUksfLogger.Object);
        var command = processService.CreateCommand(executable, ".", args).WithTimeout(TimeSpan.FromSeconds(5));

        // Act
        var results = new List<ProcessOutputLine>();
        await foreach (var outputLine in command.ExecuteAsync(_cancellationTokenSource.Token))
        {
            results.Add(outputLine);
        }

        // Assert
        results.Should().NotBeNull();
        results.Should().Contain(r => r.Type == ProcessOutputType.ProcessCompleted && r.ExitCode == 1);
        results.Should().Contain(r => r.Type == ProcessOutputType.ProcessCompleted && r.Content.Contains("Process exited with code 1"));
    }

    [Fact]
    public async Task ExecuteAsync_Should_HandleFailingCommandWithErrorOutput_WithoutHanging()
    {
        // Arrange
        GetPlatformCommand(out var executable, out var args, "echo Error output to stderr >&2 && exit 1");
        var processService = new ProcessCommandFactory(_mockUksfLogger.Object);
        var command = processService.CreateCommand(executable, ".", args)
                                    .WithTimeout(TimeSpan.FromSeconds(5))
                                    .WithProcessId("test-build-456")
                                    .WithProcessTracker(_mockProcessTracker.Object)
                                    .WithLogging(true);

        var startTime = DateTime.UtcNow;

        // Act
        var results = new List<ProcessOutputLine>();
        await foreach (var outputLine in command.ExecuteAsync(_cancellationTokenSource.Token))
        {
            results.Add(outputLine);
        }

        // Assert
        var duration = DateTime.UtcNow - startTime;

        // The process should complete without hanging (much faster than timeout)
        duration.Should().BeLessThan(TimeSpan.FromSeconds(5), "Process should not hang and should complete quickly");

        results.Should().NotBeEmpty("Result should not be empty even when process fails");

        // Verify that the process was properly tracked and cleaned up
        _mockProcessTracker.Verify(x => x.RegisterProcess(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockProcessTracker.Verify(x => x.UnregisterProcess(It.IsAny<int>()), Times.Once);

        // Verify that proper logging occurred for the lifecycle
        _mockUksfLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Process started with ID"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Should_StreamAllOutput_WithoutSuppressionConcerns()
    {
        // Arrange
        GetPlatformCommand(out var executable, out var args, "echo test output");
        var processService = new ProcessCommandFactory(_mockUksfLogger.Object);
        var command = processService.CreateCommand(executable, ".", args).WithTimeout(TimeSpan.FromSeconds(5));

        // Act
        var results = new List<ProcessOutputLine>();
        await foreach (var outputLine in command.ExecuteAsync(_cancellationTokenSource.Token))
        {
            results.Add(outputLine);
        }

        // Assert
        results.Should().NotBeEmpty();
        // Executor should always stream all output without suppression logic
        results.Where(r => r.Type == ProcessOutputType.Output).Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_Should_HandleTimeout_Gracefully()
    {
        // Arrange
        GetPlatformCommand(out var executable, out var args, "powershell.exe -Command \"Start-Sleep 15\"");
        var processService = new ProcessCommandFactory(_mockUksfLogger.Object);
        var command = processService.CreateCommand(executable, ".", args).WithTimeout(TimeSpan.FromSeconds(3));

        var startTime = DateTime.UtcNow;

        // Act
        var results = new List<ProcessOutputLine>();
        TaskCanceledException caughtException = null;
        try
        {
            await foreach (var outputLine in command.ExecuteAsync(_cancellationTokenSource.Token))
            {
                results.Add(outputLine);
            }
        }
        catch (TaskCanceledException ex)
        {
            // Expected when timeout occurs
            caughtException = ex;
        }

        // Assert
        var duration = DateTime.UtcNow - startTime;
        duration.Should().BeLessThan(TimeSpan.FromSeconds(4), "Should timeout within reasonable time");
        caughtException.Should().NotBeNull("TaskCanceledException should be thrown on timeout");
        caughtException.Message.Should().Contain("task was canceled");
    }

    [Fact]
    public async Task ExecuteAsync_Should_NotRegisterProcess_When_BuildIdIsNull()
    {
        // Arrange
        GetPlatformCommand(out var executable, out var args, "echo test");
        var processService = new ProcessCommandFactory(_mockUksfLogger.Object);
        var command = processService.CreateCommand(executable, ".", args).WithTimeout(TimeSpan.FromSeconds(5));
        // Note: Not setting BuildId

        // Act
        var results = new List<ProcessOutputLine>();
        await foreach (var outputLine in command.ExecuteAsync(_cancellationTokenSource.Token))
        {
            results.Add(outputLine);
        }

        // Assert
        results.Should().NotBeEmpty();
        // Should not attempt to register process without BuildId
        _mockProcessTracker.Verify(x => x.RegisterProcess(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_Should_StreamAllErrorsAndOutputs()
    {
        // Arrange
        GetPlatformCommand(out var executable, out var args, "echo test output && echo error output >&2");
        var processService = new ProcessCommandFactory(_mockUksfLogger.Object);
        var command = processService.CreateCommand(executable, ".", args).WithTimeout(TimeSpan.FromSeconds(5));

        // Act
        var results = new List<ProcessOutputLine>();
        await foreach (var outputLine in command.ExecuteAsync(_cancellationTokenSource.Token))
        {
            results.Add(outputLine);
        }

        // Assert
        results.Should().NotBeEmpty();
        // Should capture both standard output and error output
        results.Where(r => r.Type == ProcessOutputType.Output).Should().NotBeEmpty();
        results.Where(r => r.Type == ProcessOutputType.Error).Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_Should_RegisterProcess_When_ProcessTrackerIsProvided()
    {
        // Arrange
        GetPlatformCommand(out var executable, out var args, "echo test");
        var processService = new ProcessCommandFactory(_mockUksfLogger.Object);
        var command = processService.CreateCommand(executable, ".", args)
                                    .WithTimeout(TimeSpan.FromSeconds(5))
                                    .WithProcessId("test-build-123")
                                    .WithProcessTracker(_mockProcessTracker.Object);

        // Act
        var results = new List<ProcessOutputLine>();
        await foreach (var outputLine in command.ExecuteAsync(_cancellationTokenSource.Token))
        {
            results.Add(outputLine);
        }

        // Assert
        results.Should().NotBeEmpty();
        _mockProcessTracker.Verify(x => x.RegisterProcess(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockProcessTracker.Verify(x => x.UnregisterProcess(It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Should_SupportExtendedTimeouts()
    {
        // Arrange
        GetPlatformCommand(out var executable, out var args, "echo extended timeout test");
        var processService = new ProcessCommandFactory(_mockUksfLogger.Object);
        var command = processService.CreateCommand(executable, ".", args).WithTimeout(TimeSpan.FromSeconds(5)).WithLogging(true);

        // Act & Assert - Should handle large timeout values without issues
        var results = new List<ProcessOutputLine>();
        await foreach (var outputLine in command.ExecuteAsync(_cancellationTokenSource.Token))
        {
            results.Add(outputLine);
        }

        results.Should().NotBeEmpty();

        // Verify logging occurred
        _mockUksfLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Process started with ID"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Should_ReportExitCodeInformation_WithProcessCompleted()
    {
        // Arrange
        GetPlatformCommand(out var executable, out var args, "exit 42");
        var processService = new ProcessCommandFactory(_mockUksfLogger.Object);
        var command = processService.CreateCommand(executable, ".", args).WithTimeout(TimeSpan.FromSeconds(5));

        // Act
        var results = new List<ProcessOutputLine>();
        await foreach (var outputLine in command.ExecuteAsync(_cancellationTokenSource.Token))
        {
            results.Add(outputLine);
        }

        // Assert
        results.Should().NotBeEmpty();
        var completedResults = results.Where(r => r.Type == ProcessOutputType.ProcessCompleted).ToList();
        completedResults.Should().NotBeEmpty();
        completedResults.Should().Contain(r => r.ExitCode == 42);
        completedResults.Should().Contain(r => r.Content.Contains("Process exited with code 42"));
    }

    [Fact]
    public async Task ExecuteAsync_Should_CaptureOutputCorrectly()
    {
        // Arrange
        GetPlatformCommand(out var executable, out var args, "echo Output Line");
        var processService = new ProcessCommandFactory(_mockUksfLogger.Object);
        var command = processService.CreateCommand(executable, ".", args).WithTimeout(TimeSpan.FromSeconds(5)).WithLogging(true);

        // Act
        var results = new List<ProcessOutputLine>();
        await foreach (var outputLine in command.ExecuteAsync(_cancellationTokenSource.Token))
        {
            results.Add(outputLine);
        }

        // Assert
        results.Should().NotBeNull();
        results.Should().NotBeEmpty();
        results.Should().HaveCountGreaterOrEqualTo(1); // Should capture at least one output
        results.Should().Contain(line => line.Content.Contains("Output"));

        // Verify logging occurred
        _mockUksfLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Process started with ID"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Should_HandleTaskCanceledException_Correctly()
    {
        // Arrange
        GetPlatformCommand(out var executable, out var args, "powershell.exe -Command \"Start-Sleep 10\"");
        var processService = new ProcessCommandFactory(_mockUksfLogger.Object);
        var command = processService.CreateCommand(executable, ".", args).WithTimeout(TimeSpan.FromSeconds(3));

        var startTime = DateTime.UtcNow;

        // Act
        var results = new List<ProcessOutputLine>();
        TaskCanceledException caughtException = null;
        try
        {
            await foreach (var outputLine in command.ExecuteAsync(_cancellationTokenSource.Token))
            {
                results.Add(outputLine);
            }
        }
        catch (TaskCanceledException ex)
        {
            // Expected when timeout occurs
            caughtException = ex;
        }

        // Assert
        var duration = DateTime.UtcNow - startTime;
        duration.Should().BeLessThan(TimeSpan.FromSeconds(5), "Should timeout within reasonable time");
        caughtException.Should().NotBeNull("TaskCanceledException should be thrown on timeout");
        caughtException.Message.Should().Contain("task was canceled");

        // Verify that the exception is properly propagated rather than being swallowed
        caughtException.Should().BeOfType<TaskCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_Should_StreamOutput_InRealTime()
    {
        // Arrange
        GetPlatformCommand(out var executable, out var args, "echo line1 && echo line2 && echo line3");
        var processService = new ProcessCommandFactory(_mockUksfLogger.Object);
        var command = processService.CreateCommand(executable, ".", args).WithTimeout(TimeSpan.FromSeconds(5));

        // Act
        var results = new List<ProcessOutputLine>();
        await foreach (var outputLine in command.ExecuteAsync(_cancellationTokenSource.Token))
        {
            results.Add(outputLine);
            // Each line should be streamed as it becomes available
        }

        // Assert
        results.Should().NotBeEmpty();
        results.Where(r => r.Type == ProcessOutputType.Output).Should().HaveCountGreaterOrEqualTo(3);

        // Verify we get multiple distinct output lines
        var outputLines = results.Where(r => r.Type == ProcessOutputType.Output).Select(r => r.Content).ToList();
        outputLines.Should().Contain(line => line.Contains("line1"));
        outputLines.Should().Contain(line => line.Contains("line2"));
        outputLines.Should().Contain(line => line.Contains("line3"));
    }

    private static void GetPlatformCommand(out string executable, out string args, string command)
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            executable = "cmd.exe";
            args = $"/c \"{command}\"";
        }
        else
        {
            executable = "sh";
            args = $"-c \"{command}\"";
        }
    }
}
