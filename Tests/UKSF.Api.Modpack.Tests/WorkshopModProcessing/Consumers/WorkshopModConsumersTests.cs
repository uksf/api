using FluentAssertions;
using MassTransit;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing;
using UKSF.Api.Modpack.WorkshopModProcessing.Consumers;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;
using Xunit;

namespace UKSF.Api.Modpack.Tests.WorkshopModProcessing.Consumers;

file static class TestHelpers
{
    public static Mock<ConsumeContext<TMessage>> CreateContext<TMessage>(TMessage message) where TMessage : class
    {
        var context = new Mock<ConsumeContext<TMessage>>();
        context.SetupGet(x => x.Message).Returns(message);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return context;
    }
}

public class WorkshopModDownloadConsumerTests
{
    [Theory]
    [InlineData(WorkshopModOperationType.Install)]
    [InlineData(WorkshopModOperationType.Update)]
    public async Task Consume_WhenDownloadSucceeds_ShouldPublishComplete(WorkshopModOperationType type)
    {
        var operation = new Mock<IWorkshopModOperation>();
        operation.Setup(x => x.DownloadAsync("mod1", type, It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Successful());
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModDownloadConsumer(operation.Object, logger.Object);

        var context = TestHelpers.CreateContext(new WorkshopModDownloadCommand { WorkshopModId = "mod1", OperationType = type });
        WorkshopModDownloadComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModDownloadComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModDownloadComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("mod1");
    }

    [Fact]
    public async Task Consume_WhenDownloadFails_ShouldPublishFaulted()
    {
        var operation = new Mock<IWorkshopModOperation>();
        operation.Setup(x => x.DownloadAsync("mod1", WorkshopModOperationType.Install, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(OperationResult.Failure("bad"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModDownloadConsumer(operation.Object, logger.Object);

        var context = TestHelpers.CreateContext(new WorkshopModDownloadCommand { WorkshopModId = "mod1", OperationType = WorkshopModOperationType.Install });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.FaultedState.Should().Be("Downloading");
        published.ErrorMessage.Should().Be("bad");
    }

    [Fact]
    public async Task Consume_WhenDownloadCancelled_ShouldPublishFaulted()
    {
        var operation = new Mock<IWorkshopModOperation>();
        operation.Setup(x => x.DownloadAsync("mod1", WorkshopModOperationType.Install, It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new OperationCanceledException());
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModDownloadConsumer(operation.Object, logger.Object);

        var context = TestHelpers.CreateContext(new WorkshopModDownloadCommand { WorkshopModId = "mod1", OperationType = WorkshopModOperationType.Install });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("Operation cancelled");
        published.FaultedState.Should().Be("Downloading");
    }

    [Fact]
    public async Task Consume_WhenDownloadThrows_ShouldPublishFaulted()
    {
        var operation = new Mock<IWorkshopModOperation>();
        operation.Setup(x => x.DownloadAsync("mod1", WorkshopModOperationType.Install, It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("boom"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModDownloadConsumer(operation.Object, logger.Object);

        var context = TestHelpers.CreateContext(new WorkshopModDownloadCommand { WorkshopModId = "mod1", OperationType = WorkshopModOperationType.Install });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("boom");
        published.FaultedState.Should().Be("Downloading");
    }
}

public class WorkshopModCheckConsumerTests
{
    [Fact]
    public async Task Consume_WhenCheckSucceeds_WithIntervention_ShouldPublishCheckComplete()
    {
        var operation = new Mock<IWorkshopModOperation>();
        operation.Setup(x => x.CheckAsync("mod1", WorkshopModOperationType.Install, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(OperationResult.Successful(interventionRequired: true));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModCheckConsumer(operation.Object, logger.Object);

        var context = TestHelpers.CreateContext(new WorkshopModCheckCommand { WorkshopModId = "mod1", OperationType = WorkshopModOperationType.Install });
        WorkshopModCheckComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModCheckComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModCheckComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.InterventionRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Consume_WhenCheckSucceeds_WithoutIntervention_ShouldPublishCheckCompleteWithAvailablePbos()
    {
        var availablePbos = new List<string> { "a.pbo", "b.pbo" };
        var operation = new Mock<IWorkshopModOperation>();
        operation.Setup(x => x.CheckAsync("mod1", WorkshopModOperationType.Update, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(OperationResult.Successful(interventionRequired: false, availablePbos: availablePbos));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModCheckConsumer(operation.Object, logger.Object);

        var context = TestHelpers.CreateContext(new WorkshopModCheckCommand { WorkshopModId = "mod1", OperationType = WorkshopModOperationType.Update });
        WorkshopModCheckComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModCheckComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModCheckComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.InterventionRequired.Should().BeFalse();
        published.AvailablePbos.Should().BeEquivalentTo(availablePbos);
    }

    [Fact]
    public async Task Consume_WhenCheckFails_ShouldPublishFaulted()
    {
        var operation = new Mock<IWorkshopModOperation>();
        operation.Setup(x => x.CheckAsync("mod1", WorkshopModOperationType.Install, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(OperationResult.Failure("bad"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModCheckConsumer(operation.Object, logger.Object);

        var context = TestHelpers.CreateContext(new WorkshopModCheckCommand { WorkshopModId = "mod1", OperationType = WorkshopModOperationType.Install });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.FaultedState.Should().Be("Checking");
        published.ErrorMessage.Should().Be("bad");
    }

    [Fact]
    public async Task Consume_WhenCheckCancelled_ShouldPublishFaulted()
    {
        var operation = new Mock<IWorkshopModOperation>();
        operation.Setup(x => x.CheckAsync("mod1", WorkshopModOperationType.Install, It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new OperationCanceledException());
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModCheckConsumer(operation.Object, logger.Object);

        var context = TestHelpers.CreateContext(new WorkshopModCheckCommand { WorkshopModId = "mod1", OperationType = WorkshopModOperationType.Install });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("Operation cancelled");
        published.FaultedState.Should().Be("Checking");
    }

    [Fact]
    public async Task Consume_WhenCheckThrows_ShouldPublishFaulted()
    {
        var operation = new Mock<IWorkshopModOperation>();
        operation.Setup(x => x.CheckAsync("mod1", WorkshopModOperationType.Install, It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("boom"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModCheckConsumer(operation.Object, logger.Object);

        var context = TestHelpers.CreateContext(new WorkshopModCheckCommand { WorkshopModId = "mod1", OperationType = WorkshopModOperationType.Install });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("boom");
        published.FaultedState.Should().Be("Checking");
    }
}

public class WorkshopModExecuteConsumerTests
{
    [Fact]
    public async Task Consume_WhenExecuteSucceeds_ShouldPublishCompleteWithFilesChanged()
    {
        var operation = new Mock<IWorkshopModOperation>();
        operation.Setup(x => x.ExecuteAsync("mod1", WorkshopModOperationType.Install, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(OperationResult.Successful(filesChanged: true));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModExecuteConsumer(operation.Object, logger.Object);

        var context = TestHelpers.CreateContext(
            new WorkshopModExecuteCommand
            {
                WorkshopModId = "mod1",
                OperationType = WorkshopModOperationType.Install,
                SelectedPbos = ["a.pbo"]
            }
        );
        WorkshopModExecuteComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModExecuteComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModExecuteComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("mod1");
        published.FilesChanged.Should().BeTrue();
    }

    [Fact]
    public async Task Consume_WhenExecuteFails_ShouldPublishFaulted()
    {
        var operation = new Mock<IWorkshopModOperation>();
        operation.Setup(x => x.ExecuteAsync("mod1", WorkshopModOperationType.Install, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(OperationResult.Failure("failed"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModExecuteConsumer(operation.Object, logger.Object);

        var context = TestHelpers.CreateContext(
            new WorkshopModExecuteCommand
            {
                WorkshopModId = "mod1",
                OperationType = WorkshopModOperationType.Install,
                SelectedPbos = ["a.pbo"]
            }
        );
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("failed");
        published.FaultedState.Should().Be("Executing");
    }

    [Fact]
    public async Task Consume_WhenExecuteCancelled_ShouldPublishFaulted()
    {
        var operation = new Mock<IWorkshopModOperation>();
        operation.Setup(x => x.ExecuteAsync("mod1", WorkshopModOperationType.Install, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new OperationCanceledException());
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModExecuteConsumer(operation.Object, logger.Object);

        var context = TestHelpers.CreateContext(
            new WorkshopModExecuteCommand
            {
                WorkshopModId = "mod1",
                OperationType = WorkshopModOperationType.Install,
                SelectedPbos = ["a.pbo"]
            }
        );
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("Operation cancelled");
        published.FaultedState.Should().Be("Executing");
    }

    [Fact]
    public async Task Consume_WhenExecuteThrows_ShouldPublishFaulted()
    {
        var operation = new Mock<IWorkshopModOperation>();
        operation.Setup(x => x.ExecuteAsync("mod1", WorkshopModOperationType.Install, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("boom"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModExecuteConsumer(operation.Object, logger.Object);

        var context = TestHelpers.CreateContext(
            new WorkshopModExecuteCommand
            {
                WorkshopModId = "mod1",
                OperationType = WorkshopModOperationType.Install,
                SelectedPbos = ["a.pbo"]
            }
        );
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("boom");
        published.FaultedState.Should().Be("Executing");
    }
}

public class WorkshopModUninstallConsumerTests
{
    [Fact]
    public async Task Consume_WhenUninstallSucceeds_ShouldPublishComplete()
    {
        var operation = new Mock<IUninstallOperation>();
        operation.Setup(x => x.UninstallAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Successful(filesChanged: true));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUninstallConsumer(operation.Object, logger.Object);

        var context = TestHelpers.CreateContext(new WorkshopModUninstallInternalCommand { WorkshopModId = "mod1" });
        WorkshopModUninstallComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModUninstallComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModUninstallComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("mod1");
        published.FilesChanged.Should().BeTrue();
    }

    [Fact]
    public async Task Consume_WhenUninstallSucceeds_WithNoFilesChanged_ShouldPublishFilesChangedFalse()
    {
        var operation = new Mock<IUninstallOperation>();
        operation.Setup(x => x.UninstallAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Successful(filesChanged: false));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUninstallConsumer(operation.Object, logger.Object);

        var context = TestHelpers.CreateContext(new WorkshopModUninstallInternalCommand { WorkshopModId = "mod1" });
        WorkshopModUninstallComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModUninstallComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModUninstallComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.FilesChanged.Should().BeFalse();
    }

    [Fact]
    public async Task Consume_WhenUninstallFails_ShouldPublishFaulted()
    {
        var operation = new Mock<IUninstallOperation>();
        operation.Setup(x => x.UninstallAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Failure("bad"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUninstallConsumer(operation.Object, logger.Object);

        var context = TestHelpers.CreateContext(new WorkshopModUninstallInternalCommand { WorkshopModId = "mod1" });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.FaultedState.Should().Be("Uninstalling");
        published.ErrorMessage.Should().Be("bad");
    }

    [Fact]
    public async Task Consume_WhenUninstallCancelled_ShouldPublishFaulted()
    {
        var operation = new Mock<IUninstallOperation>();
        operation.Setup(x => x.UninstallAsync("mod1", It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUninstallConsumer(operation.Object, logger.Object);

        var context = TestHelpers.CreateContext(new WorkshopModUninstallInternalCommand { WorkshopModId = "mod1" });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("Operation cancelled");
        published.FaultedState.Should().Be("Uninstalling");
    }

    [Fact]
    public async Task Consume_WhenUninstallThrows_ShouldPublishFaulted()
    {
        var operation = new Mock<IUninstallOperation>();
        operation.Setup(x => x.UninstallAsync("mod1", It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUninstallConsumer(operation.Object, logger.Object);

        var context = TestHelpers.CreateContext(new WorkshopModUninstallInternalCommand { WorkshopModId = "mod1" });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("boom");
        published.FaultedState.Should().Be("Uninstalling");
    }
}

public class WorkshopModCleanupConsumerTests
{
    [Fact]
    public async Task Consume_WhenWorkshopModMissing_ShouldPublishComplete()
    {
        var processingService = new Mock<IWorkshopModsProcessingService>();
        var context = new Mock<IWorkshopModsContext>();
        context.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>())).Returns((DomainWorkshopMod)null);
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModCleanupConsumer(processingService.Object, context.Object, logger.Object);

        var consumeContext = TestHelpers.CreateContext(new WorkshopModCleanupCommand { WorkshopModId = "mod1" });
        WorkshopModCleanupComplete published = null;
        consumeContext.Setup(x => x.Publish(It.IsAny<WorkshopModCleanupComplete>(), It.IsAny<CancellationToken>()))
                      .Callback<WorkshopModCleanupComplete, CancellationToken>((msg, _) => published = msg)
                      .Returns(Task.CompletedTask);

        await consumer.Consume(consumeContext.Object);

        published.Should().NotBeNull();
        processingService.Verify(x => x.QueueDevBuild(), Times.Never);
    }

    [Fact]
    public async Task Consume_WhenCleanupSucceeds_ShouldQueueBuildAndPublishComplete()
    {
        var processingService = new Mock<IWorkshopModsProcessingService>();
        processingService.Setup(x => x.GetWorkshopModPath("mod1")).Returns("path");
        processingService.Setup(x => x.QueueDevBuild()).Returns(Task.CompletedTask);
        var context = new Mock<IWorkshopModsContext>();
        var workshopMod = new DomainWorkshopMod { SteamId = "mod1" };
        context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModCleanupConsumer(processingService.Object, context.Object, logger.Object);

        var consumeContext = TestHelpers.CreateContext(new WorkshopModCleanupCommand { WorkshopModId = "mod1", FilesChanged = true });
        WorkshopModCleanupComplete published = null;
        consumeContext.Setup(x => x.Publish(It.IsAny<WorkshopModCleanupComplete>(), It.IsAny<CancellationToken>()))
                      .Callback<WorkshopModCleanupComplete, CancellationToken>((msg, _) => published = msg)
                      .Returns(Task.CompletedTask);

        await consumer.Consume(consumeContext.Object);

        published.Should().NotBeNull();
        processingService.Verify(x => x.CleanupWorkshopModFiles("path"), Times.Once);
        processingService.Verify(x => x.QueueDevBuild(), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenCleanupThrows_ShouldStillPublishComplete()
    {
        var processingService = new Mock<IWorkshopModsProcessingService>();
        processingService.Setup(x => x.GetWorkshopModPath("mod1")).Returns("path");
        processingService.Setup(x => x.CleanupWorkshopModFiles("path")).Throws(new IOException("fail"));
        var context = new Mock<IWorkshopModsContext>();
        var workshopMod = new DomainWorkshopMod { SteamId = "mod1" };
        context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModCleanupConsumer(processingService.Object, context.Object, logger.Object);

        var consumeContext = TestHelpers.CreateContext(new WorkshopModCleanupCommand { WorkshopModId = "mod1", FilesChanged = true });
        WorkshopModCleanupComplete published = null;
        consumeContext.Setup(x => x.Publish(It.IsAny<WorkshopModCleanupComplete>(), It.IsAny<CancellationToken>()))
                      .Callback<WorkshopModCleanupComplete, CancellationToken>((msg, _) => published = msg)
                      .Returns(Task.CompletedTask);

        await consumer.Consume(consumeContext.Object);

        published.Should().NotBeNull();
    }

    [Fact]
    public async Task Consume_WhenFilesChangedTrue_QueuesDevBuild()
    {
        var processingService = new Mock<IWorkshopModsProcessingService>();
        processingService.Setup(x => x.GetWorkshopModPath("mod1")).Returns("path");
        processingService.Setup(x => x.QueueDevBuild()).Returns(Task.CompletedTask);
        var context = new Mock<IWorkshopModsContext>();
        var workshopMod = new DomainWorkshopMod { SteamId = "mod1" };
        context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModCleanupConsumer(processingService.Object, context.Object, logger.Object);

        var consumeContext = TestHelpers.CreateContext(new WorkshopModCleanupCommand { WorkshopModId = "mod1", FilesChanged = true });
        consumeContext.Setup(x => x.Publish(It.IsAny<WorkshopModCleanupComplete>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await consumer.Consume(consumeContext.Object);

        processingService.Verify(x => x.QueueDevBuild(), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenFilesChangedFalse_DoesNotQueueDevBuild()
    {
        var processingService = new Mock<IWorkshopModsProcessingService>();
        processingService.Setup(x => x.GetWorkshopModPath("mod1")).Returns("path");
        var context = new Mock<IWorkshopModsContext>();
        var workshopMod = new DomainWorkshopMod { SteamId = "mod1" };
        context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModCleanupConsumer(processingService.Object, context.Object, logger.Object);

        var consumeContext = TestHelpers.CreateContext(new WorkshopModCleanupCommand { WorkshopModId = "mod1", FilesChanged = false });
        consumeContext.Setup(x => x.Publish(It.IsAny<WorkshopModCleanupComplete>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await consumer.Consume(consumeContext.Object);

        processingService.Verify(x => x.QueueDevBuild(), Times.Never);
    }
}
