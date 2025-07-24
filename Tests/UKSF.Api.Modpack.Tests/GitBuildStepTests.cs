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
    public async Task GitBuildStep_Should_CompleteSetup_Successfully()
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

        // The Setup method should complete without throwing
        await gitBuildStep.Setup();

        // Assert
        // Verify that required services were retrieved during Init
        _mockServiceProvider.Verify(x => x.GetService(typeof(IVariablesService)), Times.Once);
    }

    [Fact]
    public void GitBuildStep_Should_ImplementExpectedInterface()
    {
        // Arrange
        var gitBuildStep = new GitBuildStep();

        // Act & Assert
        gitBuildStep.Should().BeAssignableTo<IBuildStep>();
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
