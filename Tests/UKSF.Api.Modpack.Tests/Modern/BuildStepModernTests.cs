using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.BuildProcess;
using UKSF.Api.Modpack.BuildProcess.Modern;
using UKSF.Api.Modpack.BuildProcess.Steps;
using UKSF.Api.Modpack.Models;
using Xunit;

namespace UKSF.Api.Modpack.Tests.Modern;

public class BuildStepModernTests
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Mock<IBuildProcessHelperFactory> _mockProcessHelperFactory = new();
    private readonly Mock<IBuildProcessTracker> _mockProcessTracker = new();
    private readonly Mock<IStepLogger> _mockStepLogger = new();
    private readonly Mock<IUksfLogger> _mockUksfLogger = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private ModpackBuildStep _modpackBuildStep;

    public BuildStepModernTests()
    {
        // Setup default behavior for BUILD_FORCE_LOGS variable
        var buildForceLogsVariable = new DomainVariableItem { Key = "BUILD_FORCE_LOGS", Item = false };
        _mockVariablesService.Setup(x => x.GetVariable("BUILD_FORCE_LOGS")).Returns(buildForceLogsVariable);

        // Setup default behavior for BUILD_STATE_UPDATE_INTERVAL variable
        var buildStateUpdateIntervalVariable = new DomainVariableItem { Key = "BUILD_STATE_UPDATE_INTERVAL", Item = 1.0 };
        _mockVariablesService.Setup(x => x.GetVariable("BUILD_STATE_UPDATE_INTERVAL")).Returns(buildStateUpdateIntervalVariable);
    }

    [Fact]
    public async Task RunProcessModern_Should_HandleCancellation_When_CancellationTokenIsTriggered()
    {
        // Arrange
        var buildStep = CreateTestBuildStep();
        var preCancelledTokenSource = new CancellationTokenSource();
        preCancelledTokenSource.Cancel(); // Pre-cancel the token

        InitializeBuildStep(buildStep, preCancelledTokenSource);

        string executable, args;
        GetPlatformCommand(out executable, out args, "echo cancellation test");

        // Act & Assert
        // With a pre-cancelled token, we expect a TaskCanceledException
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await buildStep.RunProcessModern(".", executable, args, 5000, log: true, raiseErrors: false);
            }
        );
    }

    [Fact]
    public async Task RunProcessModern_Should_HandleErrorSilently_When_ErrorSilentlyIsTrue()
    {
        // Arrange
        var buildStep = CreateTestBuildStep();
        InitializeBuildStep(buildStep);

        string executable, args;
        GetPlatformCommand(out executable, out args, "exit 1");

        // Act
        var result = await buildStep.RunProcessModern(".", executable, args, 5000, raiseErrors: false, errorSilently: true);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        _mockStepLogger.Verify(x => x.LogError(It.IsAny<Exception>()), Times.Never);
    }

    [Fact]
    public async Task RunProcessModern_Should_HandleFailingCommandWithErrorOutput_WithoutHanging()
    {
        // Arrange
        var buildStep = CreateTestBuildStep();
        InitializeBuildStep(buildStep);

        string executable, args;
        GetPlatformCommand(out executable, out args, "echo Error output to stderr >&2 && exit 1");

        var startTime = DateTime.UtcNow;

        // Act
        var result = await buildStep.RunProcessModern(
            ".",
            executable,
            args,
            10000, // 10 seconds
            log: true,
            raiseErrors: false
        );

        // Assert
        var duration = DateTime.UtcNow - startTime;

        // The process should complete without hanging (much faster than timeout)
        duration.Should().BeLessThan(TimeSpan.FromSeconds(5), "Process should not hang and should complete quickly");

        result.Should().NotBeNull("Result should not be null even when process fails");
        result.Should().NotBeEmpty();

        // Verify that the process was properly tracked and cleaned up
        _mockProcessTracker.Verify(x => x.RegisterProcess(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockProcessTracker.Verify(x => x.UnregisterProcess(It.IsAny<int>()), Times.Once);

        // Verify that proper logging occurred for the lifecycle
        _mockUksfLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Starting process"))), Times.Once);
    }

    [Fact]
    public async Task RunProcessModern_Should_HandleIgnoreErrorGates()
    {
        // Arrange
        var buildStep = CreateTestBuildStep();
        InitializeBuildStep(buildStep);

        string executable, args;
        GetPlatformCommand(out executable, out args, "echo test");

        // Act & Assert - Should not throw
        var result = await buildStep.RunProcessModern(
            ".",
            executable,
            args,
            5000,
            raiseErrors: false,
            ignoreErrorGateOpen: "START_IGNORE",
            ignoreErrorGateClose: "END_IGNORE"
        );

        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RunProcessModern_Should_HandleSuppressOutput()
    {
        // Arrange
        var buildStep = CreateTestBuildStep();
        InitializeBuildStep(buildStep);

        string executable, args;
        GetPlatformCommand(out executable, out args, "echo test output");

        // Act
        var result = await buildStep.RunProcessModern(".", executable, args, 5000, suppressOutput: true);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        // With suppressOutput, StepLogger.Log should not be called for standard output
        _mockStepLogger.Verify(x => x.Log(It.Is<string>(s => s.Contains("test output")), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RunProcessModern_Should_HandleTimeout_Gracefully()
    {
        // Arrange
        var buildStep = CreateTestBuildStep();
        InitializeBuildStep(buildStep);

        string executable, args;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            executable = "powershell.exe";
            args = "-Command \"Start-Sleep 10\""; // 10 second delay
        }
        else
        {
            executable = "sh";
            args = "-c \"sleep 10\""; // 10 second delay
        }

        var startTime = DateTime.UtcNow;

        // Act
        var result = await buildStep.RunProcessModern(
            ".",
            executable,
            args,
            1000, // 1 second timeout
            raiseErrors: false
        );

        // Assert
        var duration = DateTime.UtcNow - startTime;
        duration.Should().BeLessThan(TimeSpan.FromSeconds(3), "Should timeout quickly");
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RunProcessModern_Should_NotThrowException_When_RaiseErrorsIsFalse()
    {
        // Arrange
        var buildStep = CreateTestBuildStep();
        InitializeBuildStep(buildStep);

        string executable, args;
        GetPlatformCommand(out executable, out args, "exit 42");

        // Act & Assert - Should not throw
        var result = await buildStep.RunProcessModern(".", executable, args, 5000, raiseErrors: false);

        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RunProcessModern_Should_ThrowException_WithCorrectExitCode_When_ProcessExitsWithNonZeroCode()
    {
        // Arrange
        var buildStep = CreateTestBuildStep();
        InitializeBuildStep(buildStep);

        string executable, args;
        GetPlatformCommand(out executable, out args, "exit 42");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(async () => { await buildStep.RunProcessModern(".", executable, args, 5000, raiseErrors: true); });

        exception.Message.Should().Contain("Process failed with exit code 42");
    }

    [Fact]
    public async Task RunProcessModern_Should_CaptureOutputCorrectly()
    {
        // Arrange
        var buildStep = CreateTestBuildStep();
        InitializeBuildStep(buildStep);

        string executable, args;
        GetPlatformCommand(out executable, out args, "echo Output Line");

        // Act
        var result = await buildStep.RunProcessModern(".", executable, args, 5000, log: true, raiseErrors: false);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().HaveCountGreaterOrEqualTo(1); // Should capture at least one output
        result.Should().Contain(line => line.Contains("Output"));

        // Verify logging occurred - check the actual logs in the build step
        _modpackBuildStep.Logs.Should().NotBeEmpty();
        _modpackBuildStep.Logs.Should().Contain(log => log.Text.Contains("Output"));

        _mockUksfLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Starting process"))), Times.Once);
    }

    [Theory]
    [InlineData("error1", "error2")]
    [InlineData("warning", "critical")]
    public async Task RunProcessModern_Should_HandleErrorExclusions(string exclusion1, string exclusion2)
    {
        // Arrange
        var errorExclusions = new List<string> { exclusion1, exclusion2 };
        var buildStep = CreateTestBuildStep();
        InitializeBuildStep(buildStep);

        string executable, args;
        GetPlatformCommand(out executable, out args, "echo test");

        // Act & Assert - Should not throw
        var result = await buildStep.RunProcessModern(".", executable, args, 5000, raiseErrors: false, errorExclusions: errorExclusions);

        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RunProcessModern_Should_HandleTimeout_AndThrowTimeoutException_When_RaiseErrorsIsTrue()
    {
        // Arrange
        var buildStep = CreateTestBuildStep();
        InitializeBuildStep(buildStep);

        string executable, args;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            executable = "powershell.exe";
            args = "-Command \"Start-Sleep 10\""; // 10 second delay
        }
        else
        {
            executable = "sh";
            args = "-c \"sleep 10\""; // 10 second delay
        }

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await buildStep.RunProcessModern(
                    ".",
                    executable,
                    args,
                    1000, // 1 second timeout
                    raiseErrors: true
                );
            }
        );

        exception.Message.Should().Contain("Process execution timed out after");
    }

    [Fact]
    public async Task RunProcessModern_Should_LogToStepLogger_WithCorrectColors()
    {
        // Arrange
        var buildStep = CreateTestBuildStep();
        InitializeBuildStep(buildStep);

        string executable, args;
        GetPlatformCommand(out executable, out args, "echo Test Output");

        // Act
        var result = await buildStep.RunProcessModern(".", executable, args, 5000, log: true);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();

        // Verify that StepLogger.Log was called for standard output - check the actual logs
        _modpackBuildStep.Logs.Should().NotBeEmpty();
        _modpackBuildStep.Logs.Should().Contain(log => log.Text.Contains("Test Output"));
    }

    [Fact]
    public async Task RunProcessModern_Should_SupportExtendedTimeouts()
    {
        // Arrange
        var buildStep = CreateTestBuildStep();
        InitializeBuildStep(buildStep);

        string executable, args;
        GetPlatformCommand(out executable, out args, "echo extended timeout test");

        var twoMinutesInMs = (int)TimeSpan.FromMinutes(2).TotalMilliseconds;

        // Act & Assert - Should handle large timeout values without issues
        var result = await buildStep.RunProcessModern(".", executable, args, twoMinutesInMs, log: true, raiseErrors: false);

        result.Should().NotBeNull();
        result.Should().NotBeEmpty();

        // Verify logging occurred
        _mockUksfLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Starting process"))), Times.Once);
    }

    [Fact]
    public async Task RunProcessModern_Should_ReturnSameSignature_AsRunProcess()
    {
        // Arrange
        var buildStep = CreateTestBuildStep();
        InitializeBuildStep(buildStep);

        string executable, args;
        GetPlatformCommand(out executable, out args, "echo signature test");

        // Act
        var result = await buildStep.RunProcessModern(
            ".",
            executable,
            args,
            5000,
            log: false,
            suppressOutput: false,
            raiseErrors: true,
            errorSilently: false,
            errorExclusions: null,
            ignoreErrorGateClose: "",
            ignoreErrorGateOpen: ""
        );

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<string>>();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RunProcessModern_Should_NotThrowException_When_RaiseErrorsIsFalse_AndExitCodeIsNonZero()
    {
        // Arrange
        var buildStep = CreateTestBuildStep();
        InitializeBuildStep(buildStep);

        string executable, args;
        GetPlatformCommand(out executable, out args, "exit 1");

        // Act & Assert - Should not throw when raiseErrors is false
        var result = await buildStep.RunProcessModern(
            ".",
            executable,
            args,
            5000,
            raiseErrors: false // This should prevent exceptions from being thrown
        );

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();

        // When raiseErrors is false, exit codes should not be treated as errors
        // The process completion should be captured in the results but not logged as an error
        result.Should().Contain(line => line.Contains("Process exited with code 1"));

        // Verify no error was logged to the step logger when raiseErrors is false
        _mockStepLogger.Verify(x => x.LogError(It.IsAny<Exception>()), Times.Never);
    }

    private TestBuildStep CreateTestBuildStep()
    {
        return new TestBuildStep();
    }

    private void InitializeBuildStep(TestBuildStep buildStep, CancellationTokenSource cancellationTokenSource = null)
    {
        var serviceProvider = CreateServiceProvider();
        var modpackBuild = new DomainModpackBuild
        {
            Id = "test-build-123",
            Environment = GameEnvironment.Development,
            EnvironmentVariables = new Dictionary<string, object>()
        };
        _modpackBuildStep = new ModpackBuildStep("Test Step") { Logs = new List<ModpackBuildStepLogItem>() };

        buildStep.Init(
            serviceProvider,
            _mockUksfLogger.Object,
            modpackBuild,
            _modpackBuildStep,
            async updateDefinition => await Task.CompletedTask,
            async () => await Task.CompletedTask,
            cancellationTokenSource ?? _cancellationTokenSource
        );
    }

    private IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton(_mockVariablesService.Object);
        services.AddSingleton(_mockProcessHelperFactory.Object);
        services.AddSingleton(new BuilderProcessExecutor(_mockUksfLogger.Object, _mockVariablesService.Object, _mockProcessTracker.Object));

        return services.BuildServiceProvider();
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

    // Test implementation of BuildStep that exposes RunProcessModern as public
    private class TestBuildStep : BuildStep
    {
        public new async Task<List<string>> RunProcessModern(
            string workingDirectory,
            string executable,
            string args,
            int timeout,
            bool log = false,
            bool suppressOutput = false,
            bool raiseErrors = true,
            bool errorSilently = false,
            List<string> errorExclusions = null,
            string ignoreErrorGateClose = "",
            string ignoreErrorGateOpen = ""
        )
        {
            return await base.RunProcessModern(
                workingDirectory,
                executable,
                args,
                timeout,
                log,
                suppressOutput,
                raiseErrors,
                errorSilently,
                errorExclusions,
                ignoreErrorGateClose,
                ignoreErrorGateOpen
            );
        }
    }
}
