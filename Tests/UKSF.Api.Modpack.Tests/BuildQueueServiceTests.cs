using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Modpack.BuildProcess;
using UKSF.Api.Modpack.Models;
using Xunit;

namespace UKSF.Api.Modpack.Tests;

public class BuildQueueServiceTests
{
    private const int CleanupDelaySeconds = 1; // Use a short delay for tests
    private readonly BuildQueueService _buildQueueService;
    private readonly Mock<IBuildProcessorService> _mockBuildProcessorService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();

    public BuildQueueServiceTests()
    {
        _buildQueueService = new BuildQueueService(_mockBuildProcessorService.Object, _mockLogger.Object, CleanupDelaySeconds);
    }

    [Fact]
    public async Task WhenCancellingAllBuilds_ShouldClearQueueAndCancelRunning()
    {
        // Arrange
        var build1 = new DomainModpackBuild { Id = "testId1" };
        var build2 = new DomainModpackBuild { Id = "testId2" };
        var build3 = new DomainModpackBuild { Id = "testId3" };

        var tcs = new TaskCompletionSource<bool>();

        _mockBuildProcessorService.Setup(x => x.ProcessBuildWithErrorHandling(It.IsAny<DomainModpackBuild>(), It.IsAny<CancellationTokenSource>()))
                                  .Returns((DomainModpackBuild b, CancellationTokenSource cts) =>
                                      {
                                          cts.Token.Register(() =>
                                              {
                                                  if (b.Id == "testId1")
                                                  {
                                                      tcs.SetResult(true);
                                                  }
                                              }
                                          );
                                          return Task.Delay(10000, cts.Token).ContinueWith(_ => { }, cts.Token);
                                      }
                                  );

        // Queue the builds
        _buildQueueService.QueueBuild(build1);
        _buildQueueService.QueueBuild(build2);
        _buildQueueService.QueueBuild(build3);

        // Let the first one start processing
        await Task.Delay(300);

        // Act
        await _buildQueueService.CancelAll();

        // Assert - verify queue is cleared by checking we can't cancel the queued items
        _buildQueueService.CancelQueued("testId2").Should().BeFalse("the queue should be cleared");
        _buildQueueService.CancelQueued("testId3").Should().BeFalse("the queue should be cleared");

        // Verify running build was cancelled
        await Task.WhenAny(tcs.Task, Task.Delay(1000));
        tcs.Task.IsCompleted.Should().BeTrue("the running build's token should be cancelled");
    }

    [Fact]
    public void WhenCancellingNonExistentQueuedBuild_ShouldReturnFalse()
    {
        // Act
        var result = _buildQueueService.CancelQueued("nonExistentId");

        // Assert
        result.Should().BeFalse("the build doesn't exist in the queue");
    }

    [Fact]
    public async Task WhenCancellingQueuedBuild_ShouldRemoveFromQueue()
    {
        // Arrange
        var build = new DomainModpackBuild { Id = "testId" };

        // Set up a delayed task to simulate a long-running process
        _mockBuildProcessorService.Setup(x => x.ProcessBuildWithErrorHandling(It.IsAny<DomainModpackBuild>(), It.IsAny<CancellationTokenSource>()))
                                  .Returns(Task.Delay(5000));

        // Add a build that will block the queue
        var blockingBuild = new DomainModpackBuild { Id = "blockingBuild" };
        var blockingTcs = new TaskCompletionSource<bool>();

        _mockBuildProcessorService
            .Setup(x => x.ProcessBuildWithErrorHandling(It.Is<DomainModpackBuild>(b => b.Id == "blockingBuild"), It.IsAny<CancellationTokenSource>()))
            .Returns(() => blockingTcs.Task);

        // Queue the blocking build first
        _buildQueueService.QueueBuild(blockingBuild);
        await Task.Delay(100); // Wait for processing to start

        // Now queue our test build which should remain in the queue
        _buildQueueService.QueueBuild(build);
        await Task.Delay(100); // Small delay to ensure build is queued

        // Act - Cancel before it's processed
        var result = _buildQueueService.CancelQueued("testId");

        // Clean up
        blockingTcs.SetResult(true);

        // Assert
        result.Should().BeTrue("the build should be found and cancelled");
    }

    [Fact]
    public async Task WhenCancellingRunningBuild_ShouldCancelTokenSource()
    {
        // Arrange
        var build = new DomainModpackBuild { Id = "testId" };
        var tokenCancelled = false;
        var taskCompletionSource = new TaskCompletionSource<bool>();

        _mockBuildProcessorService.Setup(x => x.ProcessBuildWithErrorHandling(It.IsAny<DomainModpackBuild>(), It.IsAny<CancellationTokenSource>()))
                                  .Returns((DomainModpackBuild _, CancellationTokenSource cts) =>
                                      {
                                          cts.Token.Register(() =>
                                              {
                                                  tokenCancelled = true;
                                                  taskCompletionSource.SetResult(true);
                                              }
                                          );
                                          return Task.Delay(10000, cts.Token).ContinueWith(_ => { }, cts.Token);
                                      }
                                  );

        // Queue and wait for processing to start
        _buildQueueService.QueueBuild(build);
        await Task.Delay(300); // Give more time for processing to start

        // Act
        _buildQueueService.CancelRunning("testId");

        // Assert
        await Task.WhenAny(taskCompletionSource.Task, Task.Delay(1000));
        tokenCancelled.Should().BeTrue("the cancellation token should be triggered");
    }

    [Fact]
    public async Task WhenCancellingRunningBuild_ShouldCleanupAfterCompletion()
    {
        // Arrange
        var build = new DomainModpackBuild { Id = "testId" };
        var tcs = new TaskCompletionSource<bool>();
        var completionSource = new TaskCompletionSource<bool>();

        _mockBuildProcessorService.Setup(x => x.ProcessBuildWithErrorHandling(It.IsAny<DomainModpackBuild>(), It.IsAny<CancellationTokenSource>()))
                                  .Returns((DomainModpackBuild _, CancellationTokenSource cts) =>
                                      {
                                          // We need this task to be properly cancellable AND complete
                                          cts.Token.Register(() =>
                                              {
                                                  tcs.SetResult(true); // Signal cancellation received
                                                  completionSource.SetResult(true); // Also complete the task
                                              }
                                          );
                                          return completionSource.Task;
                                      }
                                  );

        // Queue and let processing start
        _buildQueueService.QueueBuild(build);
        await Task.Delay(500); // Give more time for processing to start

        // Act
        _buildQueueService.CancelRunning("testId");

        // Assert - first verify cancellation was received
        await Task.WhenAny(tcs.Task, Task.Delay(1000));
        tcs.Task.IsCompleted.Should().BeTrue("the cancellation token should be triggered");

        // Wait for the cleanup to occur - using our custom short delay
        await Task.Delay(TimeSpan.FromSeconds(CleanupDelaySeconds).Add(TimeSpan.FromMilliseconds(200)));

        // Queue a new build to check if the service continues to work properly
        var newBuild = new DomainModpackBuild { Id = "testId2" };
        var newTcs = new TaskCompletionSource<bool>();

        _mockBuildProcessorService
            .Setup(x => x.ProcessBuildWithErrorHandling(It.Is<DomainModpackBuild>(b => b.Id == "testId2"), It.IsAny<CancellationTokenSource>()))
            .Returns(Task.CompletedTask)
            .Callback(() => newTcs.SetResult(true));

        _buildQueueService.QueueBuild(newBuild);

        await Task.WhenAny(newTcs.Task, Task.Delay(1000));
        newTcs.Task.IsCompleted.Should().BeTrue("the service should continue to process new builds");
    }

    [Fact]
    public async Task WhenCancellingRunningBuildThatDoesNotComplete_ShouldLogWarning()
    {
        // Arrange
        var build = new DomainModpackBuild { Id = "testId" };

        // Clear any previous logger invocations
        _mockLogger.Invocations.Clear();

        // Create a task that genuinely never completes, even when cancelled
        var neverCompletingTcs = new TaskCompletionSource<bool>();
        _mockBuildProcessorService.Setup(x => x.ProcessBuildWithErrorHandling(It.IsAny<DomainModpackBuild>(), It.IsAny<CancellationTokenSource>()))
                                  .Returns(neverCompletingTcs.Task); // This task will never complete

        // Queue and let processing start
        _buildQueueService.QueueBuild(build);
        await Task.Delay(500); // Give more time for processing to start

        // Act
        _buildQueueService.CancelRunning("testId");

        // Assert
        await Task.Delay(TimeSpan.FromSeconds(CleanupDelaySeconds).Add(TimeSpan.FromSeconds(1)));

        _mockLogger.Verify(x => x.LogWarning(It.Is<string>(s => s.Contains("testId") && s.Contains("cancelled") && s.Contains("not completed"))), Times.Once);
    }

    [Fact]
    public async Task WhenQueueingBuild_ShouldProcessQueue()
    {
        // Arrange
        var build = new DomainModpackBuild { Id = "testId" };
        var taskCompletionSource = new TaskCompletionSource<bool>();
        _mockBuildProcessorService.Setup(x => x.ProcessBuildWithErrorHandling(It.IsAny<DomainModpackBuild>(), It.IsAny<CancellationTokenSource>()))
                                  .Returns(Task.CompletedTask)
                                  .Callback(() => taskCompletionSource.SetResult(true));

        // Act
        _buildQueueService.QueueBuild(build);

        // Assert
        await Task.WhenAny(taskCompletionSource.Task, Task.Delay(5000));
        taskCompletionSource.Task.IsCompleted.Should().BeTrue("the build processor should be called");
        _mockBuildProcessorService.Verify(
            x => x.ProcessBuildWithErrorHandling(It.Is<DomainModpackBuild>(b => b.Id == "testId"), It.IsAny<CancellationTokenSource>()),
            Times.Once
        );
    }

    [Fact]
    public async Task WhenQueueingMultipleBuilds_ShouldProcessAllInQueue()
    {
        // Arrange
        var build1 = new DomainModpackBuild { Id = "testId1" };
        var build2 = new DomainModpackBuild { Id = "testId2" };
        var build3 = new DomainModpackBuild { Id = "testId3" };
        var processCount = 0;
        var allProcessedTcs = new TaskCompletionSource<bool>();

        _mockBuildProcessorService.Setup(x => x.ProcessBuildWithErrorHandling(It.IsAny<DomainModpackBuild>(), It.IsAny<CancellationTokenSource>()))
                                  .Returns(Task.CompletedTask)
                                  .Callback(() =>
                                      {
                                          processCount++;
                                          if (processCount == 3)
                                          {
                                              allProcessedTcs.SetResult(true);
                                          }
                                      }
                                  );

        // Act
        _buildQueueService.QueueBuild(build1);
        await Task.Delay(50); // Small delay between queueing builds for better test stability
        _buildQueueService.QueueBuild(build2);
        await Task.Delay(50); // Small delay between queueing builds for better test stability
        _buildQueueService.QueueBuild(build3);

        // Assert
        await Task.WhenAny(allProcessedTcs.Task, Task.Delay(5000));
        allProcessedTcs.Task.IsCompleted.Should().BeTrue("all builds should be processed");
        _mockBuildProcessorService.Verify(
            x => x.ProcessBuildWithErrorHandling(It.IsAny<DomainModpackBuild>(), It.IsAny<CancellationTokenSource>()),
            Times.Exactly(3)
        );
    }
}
