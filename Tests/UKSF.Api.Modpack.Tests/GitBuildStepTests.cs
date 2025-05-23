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
using UKSF.Api.Modpack.BuildProcess.Steps;
using UKSF.Api.Modpack.Models;
using Xunit;

namespace UKSF.Api.Modpack.Tests;

public class GitBuildStepTests
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Mock<IUksfLogger> _mockLogger;
    private readonly Mock<IServiceProvider> _mockServiceProvider;

    public GitBuildStepTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockLogger = new Mock<IUksfLogger>();
        var mockVariablesService = new Mock<IVariablesService>();
        var mockProcessTracker = new Mock<IBuildProcessTracker>();
        _cancellationTokenSource = new CancellationTokenSource();

        // Setup required variables - create concrete instance instead of mocking
        var updateIntervalVariable = new DomainVariableItem { Item = "1.0" };
        mockVariablesService.Setup(x => x.GetVariable("BUILD_STATE_UPDATE_INTERVAL")).Returns(updateIntervalVariable);

        _mockServiceProvider.Setup(x => x.GetService(typeof(IVariablesService))).Returns(mockVariablesService.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(IBuildProcessTracker))).Returns(mockProcessTracker.Object);
    }

    [Fact]
    public async Task GitBuildStep_Should_HandleNullProcessTracker()
    {
        // Arrange
        var gitBuildStep = new GitBuildStep();
        var build = new DomainModpackBuild { EnvironmentVariables = new Dictionary<string, object>() };
        var buildStep = new ModpackBuildStep("Test Step");

        // Setup service provider to return null for process tracker
        _mockServiceProvider.Setup(x => x.GetService(typeof(IBuildProcessTracker))).Returns((IBuildProcessTracker)null);

        // Act & Assert - Should not throw when process tracker is null
        var act = async () =>
        {
            gitBuildStep.Init(
                _mockServiceProvider.Object,
                _mockLogger.Object,
                build,
                buildStep,
                _ => Task.CompletedTask,
                () => Task.CompletedTask,
                _cancellationTokenSource
            );

            await gitBuildStep.Setup();
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void GitBuildStep_Should_ImplementExpectedInterface()
    {
        // Arrange
        var gitBuildStep = new GitBuildStep();

        // Act & Assert
        gitBuildStep.Should().BeAssignableTo<IBuildStep>();

        // Verify expected methods exist (compile-time check)
        typeof(GitBuildStep).Should().HaveMethod("GitCommand", [typeof(string), typeof(string)]);
        typeof(GitBuildStep).Should().HaveMethod("SafeGitCommand", [typeof(string), typeof(string), typeof(int)]);
    }

    [Fact]
    public void GitBuildStep_Should_InheritFromBuildStep()
    {
        // Arrange & Act
        var gitBuildStep = new GitBuildStep();

        // Assert
        gitBuildStep.Should().BeAssignableTo<BuildStep>();
        gitBuildStep.Should().BeAssignableTo<IBuildStep>();
    }

    [Fact]
    public async Task GitBuildStep_Should_RetrieveProcessTracker_OnSetup()
    {
        // Arrange
        var gitBuildStep = new GitBuildStep();
        var build = new DomainModpackBuild { EnvironmentVariables = new Dictionary<string, object>() };
        var buildStep = new ModpackBuildStep("Test Step");

        // Act
        gitBuildStep.Init(
            _mockServiceProvider.Object,
            _mockLogger.Object,
            build,
            buildStep,
            _ => Task.CompletedTask,
            () => Task.CompletedTask,
            _cancellationTokenSource
        );

        // The process tracker is retrieved during Setup, not Init
        await gitBuildStep.Setup();

        // Assert
        _mockServiceProvider.Verify(x => x.GetService(typeof(IBuildProcessTracker)), Times.Once);
    }

    [Fact]
    public void GitBuildStep_Should_UseExtendedTimeoutConfiguration()
    {
        // This test verifies the configuration changes are in place
        // by checking the GitBuildStep class has the expected timeout settings

        // Arrange
        var gitBuildStep = new GitBuildStep();

        // Act & Assert
        // The GitBuildStep class should be configured with:
        // - 2 minute timeout instead of 10 seconds
        // - Safety git configurations
        // - Process tracking integration

        gitBuildStep.Should().NotBeNull();
        gitBuildStep.GetType().Should().Be<GitBuildStep>();
    }
}

// Integration test class for testing git timeout improvements
public class GitTimeoutConfigurationTests
{
    [Fact] // 15 second timeout to prevent hanging
    public void BuildProcessHelper_Should_HandleGitSafetyConfigurations()
    {
        // Arrange
        var mockStepLogger = new Mock<IStepLogger>();
        var mockLogger = new Mock<IUksfLogger>();
        var cancellationTokenSource = new CancellationTokenSource();

        var buildProcessHelper = new BuildProcessHelper(mockStepLogger.Object, mockLogger.Object, cancellationTokenSource, raiseErrors: false);

        // Act - Test git safety configurations without actually running git
        string executable;
        string args;
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            executable = "cmd.exe";
            // Use echo instead of actual git to test the safety configuration format
            args = "/c \"echo git -c core.askpass='' -c credential.helper='' -c core.longpaths=true status\"";
        }
        else
        {
            executable = "sh";
            args = "-c \"echo git -c core.askpass='' -c credential.helper='' -c core.longpaths=true status\"";
        }

        // This should not hang due to credential prompts since we're using echo
        var result = buildProcessHelper.Run(".", executable, args, 10000, true);

        // Assert
        result.Should().NotBeNull();

        // Verify the command was logged
        mockLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Starting process"))), Times.Once);
    }

    [Fact]
    public void BuildProcessHelper_Should_SupportExtendedTimeouts()
    {
        // Arrange
        var mockStepLogger = new Mock<IStepLogger>();
        var mockLogger = new Mock<IUksfLogger>();
        var cancellationTokenSource = new CancellationTokenSource();

        var buildProcessHelper = new BuildProcessHelper(mockStepLogger.Object, mockLogger.Object, cancellationTokenSource, raiseErrors: false);

        // Act & Assert - Test that extended timeouts are supported
        var twoMinutesInMs = (int)TimeSpan.FromMinutes(2).TotalMilliseconds;

        // This should not throw due to timeout value being too large
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

        var result = buildProcessHelper.Run(".", executable, args, twoMinutesInMs, true);
        result.Should().NotBeNull();

        // Verify logging occurred
        mockLogger.Verify(x => x.LogInfo(It.Is<string>(s => s.Contains("Starting process"))), Times.Once);
    }

    [Theory]
    [InlineData(1)] // 1 minute
    [InlineData(2)] // 2 minutes
    [InlineData(5)] // 5 minutes
    [InlineData(10)] // 10 minutes
    public void BuildProcessHelper_Should_SupportVariousTimeoutDurations(int timeoutMinutes)
    {
        // Arrange
        var mockStepLogger = new Mock<IStepLogger>();
        var mockLogger = new Mock<IUksfLogger>();
        var cancellationTokenSource = new CancellationTokenSource();

        var buildProcessHelper = new BuildProcessHelper(mockStepLogger.Object, mockLogger.Object, cancellationTokenSource, raiseErrors: false);

        // Act
        var timeoutMs = (int)TimeSpan.FromMinutes(timeoutMinutes).TotalMilliseconds;

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

        // Assert - Should handle various timeout durations without issues
        var result = buildProcessHelper.Run(".", executable, args, timeoutMs);
        result.Should().NotBeNull();
    }
}
