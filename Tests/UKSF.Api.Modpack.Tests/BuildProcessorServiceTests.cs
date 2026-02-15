using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Driver;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Modpack.BuildProcess;
using UKSF.Api.Modpack.BuildProcess.Steps;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using Xunit;

namespace UKSF.Api.Modpack.Tests;

public class BuildProcessorServiceTests
{
    private readonly Mock<IBuildStepService> _mockBuildStepService = new();
    private readonly Mock<IBuildsService> _mockBuildsService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IServiceProvider> _mockServiceProvider = new();
    private readonly BuildProcessorService _subject;

    public BuildProcessorServiceTests()
    {
        _subject = new BuildProcessorService(_mockServiceProvider.Object, _mockBuildStepService.Object, _mockBuildsService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessBuild_ShouldFailBuild_WhenStepThrowsOperationCanceledException_NotFromUserCancellation()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var build = new DomainModpackBuild
        {
            Id = "build-1",
            Environment = GameEnvironment.Development,
            Steps = [new ModpackBuildStep("Test Step")]
        };

        var mockStep = new Mock<IBuildStep>();
        mockStep.Setup(x => x.Start()).Returns(Task.CompletedTask);
        mockStep.Setup(x => x.CheckGuards()).Returns(true);
        mockStep.Setup(x => x.Setup()).Returns(Task.CompletedTask);
        // Simulate a timeout throwing OperationCanceledException (NOT user cancellation)
        mockStep.Setup(x => x.Process()).ThrowsAsync(new OperationCanceledException("Process execution timed out"));
        mockStep.Setup(x => x.Fail(It.IsAny<Exception>())).Returns(Task.CompletedTask);
        mockStep.Setup(x => x.Cancel()).Returns(Task.CompletedTask);

        _mockBuildStepService.Setup(x => x.ResolveBuildStep("Test Step")).Returns(mockStep.Object);

        // Act
        await _subject.ProcessBuildWithErrorHandling(build, cancellationTokenSource);

        // Assert - should call FailBuild, NOT CancelBuild
        _mockBuildsService.Verify(x => x.FailBuild(build), Times.Once);
        _mockBuildsService.Verify(x => x.CancelBuild(build), Times.Never);
        mockStep.Verify(x => x.Fail(It.IsAny<Exception>()), Times.Once);
        mockStep.Verify(x => x.Cancel(), Times.Never);
    }

    [Fact]
    public async Task ProcessBuild_ShouldCancelBuild_WhenUserRequestsCancellation()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var build = new DomainModpackBuild
        {
            Id = "build-1",
            Environment = GameEnvironment.Development,
            Steps = [new ModpackBuildStep("Test Step")]
        };

        var mockStep = new Mock<IBuildStep>();
        mockStep.Setup(x => x.Start()).Returns(Task.CompletedTask);
        mockStep.Setup(x => x.CheckGuards()).Returns(true);
        mockStep.Setup(x => x.Setup()).Returns(Task.CompletedTask);
        mockStep.Setup(x => x.Process())
                .Returns(async () =>
                    {
                        // Simulate user cancellation during process
                        await cancellationTokenSource.CancelAsync();
                        cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    }
                );
        mockStep.Setup(x => x.Cancel()).Returns(Task.CompletedTask);

        _mockBuildStepService.Setup(x => x.ResolveBuildStep("Test Step")).Returns(mockStep.Object);

        // Act
        await _subject.ProcessBuildWithErrorHandling(build, cancellationTokenSource);

        // Assert - should call CancelBuild, NOT FailBuild
        _mockBuildsService.Verify(x => x.CancelBuild(build), Times.Once);
        _mockBuildsService.Verify(x => x.FailBuild(build), Times.Never);
        mockStep.Verify(x => x.Cancel(), Times.Once);
    }

    [Fact]
    public async Task ProcessBuild_ShouldFailBuild_WhenStepThrowsTaskCanceledException()
    {
        // Arrange - TaskCanceledException inherits from OperationCanceledException
        // This simulates an HTTP timeout or similar non-user cancellation
        var cancellationTokenSource = new CancellationTokenSource();
        var build = new DomainModpackBuild
        {
            Id = "build-1",
            Environment = GameEnvironment.Development,
            Steps = [new ModpackBuildStep("Test Step")]
        };

        var mockStep = new Mock<IBuildStep>();
        mockStep.Setup(x => x.Start()).Returns(Task.CompletedTask);
        mockStep.Setup(x => x.CheckGuards()).Returns(true);
        mockStep.Setup(x => x.Setup()).Returns(Task.CompletedTask);
        mockStep.Setup(x => x.Process()).ThrowsAsync(new TaskCanceledException("A task was canceled."));
        mockStep.Setup(x => x.Fail(It.IsAny<Exception>())).Returns(Task.CompletedTask);
        mockStep.Setup(x => x.Cancel()).Returns(Task.CompletedTask);

        _mockBuildStepService.Setup(x => x.ResolveBuildStep("Test Step")).Returns(mockStep.Object);

        // Act
        await _subject.ProcessBuildWithErrorHandling(build, cancellationTokenSource);

        // Assert - TaskCanceledException from timeout should fail, not cancel
        _mockBuildsService.Verify(x => x.FailBuild(build), Times.Once);
        _mockBuildsService.Verify(x => x.CancelBuild(build), Times.Never);
        mockStep.Verify(x => x.Fail(It.IsAny<Exception>()), Times.Once);
        mockStep.Verify(x => x.Cancel(), Times.Never);
    }

    [Fact]
    public async Task ProcessBuild_ShouldSucceedBuild_WhenAllStepsPass()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var build = new DomainModpackBuild
        {
            Id = "build-1",
            Environment = GameEnvironment.Development,
            Steps = [new ModpackBuildStep("Test Step")]
        };

        var mockStep = new Mock<IBuildStep>();
        mockStep.Setup(x => x.Start()).Returns(Task.CompletedTask);
        mockStep.Setup(x => x.CheckGuards()).Returns(true);
        mockStep.Setup(x => x.Setup()).Returns(Task.CompletedTask);
        mockStep.Setup(x => x.Process()).Returns(Task.CompletedTask);
        mockStep.Setup(x => x.Succeed()).Returns(Task.CompletedTask);

        _mockBuildStepService.Setup(x => x.ResolveBuildStep("Test Step")).Returns(mockStep.Object);

        // Act
        await _subject.ProcessBuildWithErrorHandling(build, cancellationTokenSource);

        // Assert
        _mockBuildsService.Verify(x => x.SucceedBuild(build), Times.Once);
        _mockBuildsService.Verify(x => x.FailBuild(build), Times.Never);
        _mockBuildsService.Verify(x => x.CancelBuild(build), Times.Never);
    }

    [Fact]
    public async Task ProcessBuild_ShouldFailBuild_WhenStepThrowsRegularException()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var build = new DomainModpackBuild
        {
            Id = "build-1",
            Environment = GameEnvironment.Development,
            Steps = [new ModpackBuildStep("Test Step")]
        };

        var mockStep = new Mock<IBuildStep>();
        mockStep.Setup(x => x.Start()).Returns(Task.CompletedTask);
        mockStep.Setup(x => x.CheckGuards()).Returns(true);
        mockStep.Setup(x => x.Setup()).Returns(Task.CompletedTask);
        mockStep.Setup(x => x.Process()).ThrowsAsync(new Exception("Process failed with exit code 1"));
        mockStep.Setup(x => x.Fail(It.IsAny<Exception>())).Returns(Task.CompletedTask);

        _mockBuildStepService.Setup(x => x.ResolveBuildStep("Test Step")).Returns(mockStep.Object);

        // Act
        await _subject.ProcessBuildWithErrorHandling(build, cancellationTokenSource);

        // Assert
        _mockBuildsService.Verify(x => x.FailBuild(build), Times.Once);
        _mockBuildsService.Verify(x => x.CancelBuild(build), Times.Never);
        mockStep.Verify(x => x.Fail(It.IsAny<Exception>()), Times.Once);
    }

    [Fact]
    public async Task ProcessBuild_ShouldCancelBuild_WhenCancellationRequestedBetweenSteps()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync(); // Pre-cancel

        var build = new DomainModpackBuild
        {
            Id = "build-1",
            Environment = GameEnvironment.Development,
            Steps = [new ModpackBuildStep("Test Step")]
        };

        var mockStep = new Mock<IBuildStep>();
        mockStep.Setup(x => x.Cancel()).Returns(Task.CompletedTask);

        _mockBuildStepService.Setup(x => x.ResolveBuildStep("Test Step")).Returns(mockStep.Object);

        // Act
        await _subject.ProcessBuildWithErrorHandling(build, cancellationTokenSource);

        // Assert - pre-step cancellation check should cancel
        _mockBuildsService.Verify(x => x.CancelBuild(build), Times.Once);
        _mockBuildsService.Verify(x => x.FailBuild(build), Times.Never);
    }

    [Fact]
    public async Task ProcessBuild_ShouldSkipStep_WhenGuardsReturnFalse()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        var build = new DomainModpackBuild
        {
            Id = "build-1",
            Environment = GameEnvironment.Development,
            Steps = [new ModpackBuildStep("Test Step")]
        };

        var mockStep = new Mock<IBuildStep>();
        mockStep.Setup(x => x.Start()).Returns(Task.CompletedTask);
        mockStep.Setup(x => x.CheckGuards()).Returns(false);
        mockStep.Setup(x => x.Skip()).Returns(Task.CompletedTask);

        _mockBuildStepService.Setup(x => x.ResolveBuildStep("Test Step")).Returns(mockStep.Object);

        // Act
        await _subject.ProcessBuildWithErrorHandling(build, cancellationTokenSource);

        // Assert
        _mockBuildsService.Verify(x => x.SucceedBuild(build), Times.Once);
        mockStep.Verify(x => x.Skip(), Times.Once);
        mockStep.Verify(x => x.Process(), Times.Never);
    }
}
