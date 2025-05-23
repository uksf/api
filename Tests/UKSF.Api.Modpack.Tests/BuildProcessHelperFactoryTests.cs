using System;
using System.Threading;
using FluentAssertions;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.BuildProcess;
using Xunit;

namespace UKSF.Api.Modpack.Tests;

public class BuildProcessHelperFactoryTests
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Mock<IBuildProcessTracker> _mockProcessTracker = new();
    private readonly Mock<IStepLogger> _mockStepLogger = new();
    private readonly Mock<IUksfLogger> _mockUksfLogger = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();

    [Fact]
    public void BuildProcessHelper_Should_RespectBuildForceLogsVariable_WhenFalse()
    {
        // Arrange
        var buildForceLogsVariable = new DomainVariableItem { Key = "BUILD_FORCE_LOGS", Item = false };
        _mockVariablesService.Setup(x => x.GetVariable("BUILD_FORCE_LOGS")).Returns(buildForceLogsVariable);

        var factory = new BuildProcessHelperFactory(_mockVariablesService.Object);

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

        // Act
        using var helper = factory.Create(_mockStepLogger.Object, _mockUksfLogger.Object, _cancellationTokenSource);

        var result = helper.Run(".", executable, args, 5000); // log=false, BUILD_FORCE_LOGS=false

        // Assert
        result.Should().NotBeNull();
        // Verify that no logging occurred since both log parameter and BUILD_FORCE_LOGS are false
        _mockUksfLogger.Verify(x => x.LogInfo(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void BuildProcessHelper_Should_RespectBuildForceLogsVariable_WhenTrue()
    {
        // Arrange
        var buildForceLogsVariable = new DomainVariableItem { Key = "BUILD_FORCE_LOGS", Item = true };
        _mockVariablesService.Setup(x => x.GetVariable("BUILD_FORCE_LOGS")).Returns(buildForceLogsVariable);

        var factory = new BuildProcessHelperFactory(_mockVariablesService.Object);

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

        // Act
        using var helper = factory.Create(_mockStepLogger.Object, _mockUksfLogger.Object, _cancellationTokenSource);

        var result = helper.Run(".", executable, args, 5000); // log=false, but should still log due to BUILD_FORCE_LOGS=true

        // Assert
        result.Should().NotBeNull();
        // Verify that logging occurred even though log parameter was false
        _mockUksfLogger.Verify(x => x.LogInfo(It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public void Create_Should_HandleNullProcessTracker()
    {
        // Arrange
        var buildForceLogsVariable = new DomainVariableItem { Key = "BUILD_FORCE_LOGS", Item = false };
        _mockVariablesService.Setup(x => x.GetVariable("BUILD_FORCE_LOGS")).Returns(buildForceLogsVariable);

        var factory = new BuildProcessHelperFactory(_mockVariablesService.Object);

        // Act & Assert - Should not throw
        using var helper = factory.Create(_mockStepLogger.Object, _mockUksfLogger.Object, _cancellationTokenSource);

        helper.Should().NotBeNull();
    }

    [Fact]
    public void Create_Should_ReturnBuildProcessHelper_WithCorrectDependencies()
    {
        // Arrange
        var buildForceLogsVariable = new DomainVariableItem { Key = "BUILD_FORCE_LOGS", Item = false };
        _mockVariablesService.Setup(x => x.GetVariable("BUILD_FORCE_LOGS")).Returns(buildForceLogsVariable);

        var factory = new BuildProcessHelperFactory(_mockVariablesService.Object, _mockProcessTracker.Object);

        // Act
        using var helper = factory.Create(_mockStepLogger.Object, _mockUksfLogger.Object, _cancellationTokenSource, buildId: "test-build-id");

        // Assert
        helper.Should().NotBeNull();
        helper.Should().BeOfType<BuildProcessHelper>();
    }
}
