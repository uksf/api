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
    private readonly Mock<IWorkshopModsContext> _workshopModsContext = new();
    private readonly Mock<IWorkshopModsProcessingService> _processingService = new();
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly Mock<IInstallOperation> _installOperation = new();
    private readonly Mock<IUpdateOperation> _updateOperation = new();
    private readonly WorkshopModDownloadConsumer _consumer;

    public WorkshopModDownloadConsumerTests()
    {
        _consumer = new WorkshopModDownloadConsumer(
            _installOperation.Object,
            _updateOperation.Object,
            _processingService.Object,
            _workshopModsContext.Object,
            _logger.Object
        );
    }

    [Fact]
    public async Task Consume_WhenInstallDownloadSucceeds_ShouldPublishComplete()
    {
        _installOperation.Setup(x => x.DownloadAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Successful());

        var context = TestHelpers.CreateContext(new WorkshopModDownloadCommand { WorkshopModId = "mod1", OperationType = WorkshopModOperationType.Install });
        WorkshopModDownloadComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModDownloadComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModDownloadComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await _consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("mod1");
        _installOperation.Verify(x => x.DownloadAsync("mod1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenUpdateDownloadSucceeds_ShouldPublishCompleteWithUpdateStatus()
    {
        _updateOperation.Setup(x => x.DownloadAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Successful());

        var context = TestHelpers.CreateContext(new WorkshopModDownloadCommand { WorkshopModId = "mod1", OperationType = WorkshopModOperationType.Update });
        WorkshopModDownloadComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModDownloadComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModDownloadComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await _consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("mod1");
        _updateOperation.Verify(x => x.DownloadAsync("mod1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenDownloadFails_ShouldPublishFaulted()
    {
        _installOperation.Setup(x => x.DownloadAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Failure("mod1 not found"));

        var context = TestHelpers.CreateContext(new WorkshopModDownloadCommand { WorkshopModId = "mod1", OperationType = WorkshopModOperationType.Install });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await _consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.FaultedState.Should().Be("Downloading");
        published.ErrorMessage.Should().Contain("not found");
    }
}

public class WorkshopModCheckConsumerTests
{
    private readonly Mock<IWorkshopModsContext> _workshopModsContext = new();
    private readonly Mock<IWorkshopModsProcessingService> _processingService = new();
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly Mock<IInstallOperation> _installOperation = new();
    private readonly Mock<IUpdateOperation> _updateOperation = new();
    private readonly WorkshopModCheckConsumer _consumer;

    public WorkshopModCheckConsumerTests()
    {
        _consumer = new WorkshopModCheckConsumer(
            _installOperation.Object,
            _updateOperation.Object,
            _processingService.Object,
            _workshopModsContext.Object,
            _logger.Object
        );
    }

    [Fact]
    public async Task Consume_WhenInstallCheckSucceeds_WithIntervention_ShouldPublishCheckComplete()
    {
        _installOperation.Setup(x => x.CheckAsync("mod1", It.IsAny<CancellationToken>()))
                         .ReturnsAsync(OperationResult.Successful(interventionRequired: true, availablePbos: ["new.pbo"]));

        var context = TestHelpers.CreateContext(new WorkshopModCheckCommand { WorkshopModId = "mod1", OperationType = WorkshopModOperationType.Install });
        WorkshopModCheckComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModCheckComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModCheckComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await _consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.InterventionRequired.Should().BeTrue();
        published.AvailablePbos.Should().BeEquivalentTo(["new.pbo"]);
        _installOperation.Verify(x => x.CheckAsync("mod1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenUpdateCheckSucceeds_WithoutIntervention_ShouldPublishCheckCompleteWithAvailablePbos()
    {
        var availablePbos = new List<string> { "a.pbo", "b.pbo" };
        _updateOperation.Setup(x => x.CheckAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Successful(availablePbos: availablePbos));

        var context = TestHelpers.CreateContext(new WorkshopModCheckCommand { WorkshopModId = "mod1", OperationType = WorkshopModOperationType.Update });
        WorkshopModCheckComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModCheckComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModCheckComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await _consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.InterventionRequired.Should().BeFalse();
        published.AvailablePbos.Should().BeEquivalentTo(availablePbos);
        _updateOperation.Verify(x => x.CheckAsync("mod1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenRootMod_ShouldPublishNoInterventionRequired()
    {
        _installOperation.Setup(x => x.CheckAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Successful());

        var context = TestHelpers.CreateContext(new WorkshopModCheckCommand { WorkshopModId = "mod1", OperationType = WorkshopModOperationType.Install });
        WorkshopModCheckComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModCheckComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModCheckComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await _consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.InterventionRequired.Should().BeFalse();
    }

    [Fact]
    public async Task Consume_WhenCheckFails_ShouldPublishFaulted()
    {
        _installOperation.Setup(x => x.CheckAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Failure("mod1 not found"));

        var context = TestHelpers.CreateContext(new WorkshopModCheckCommand { WorkshopModId = "mod1", OperationType = WorkshopModOperationType.Install });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await _consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.FaultedState.Should().Be("Checking");
        published.ErrorMessage.Should().Contain("not found");
    }
}

public class WorkshopModExecuteConsumerTests
{
    private readonly Mock<IWorkshopModsContext> _workshopModsContext = new();
    private readonly Mock<IWorkshopModsProcessingService> _processingService = new();
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly Mock<IInstallOperation> _installOperation = new();
    private readonly Mock<IUpdateOperation> _updateOperation = new();
    private readonly WorkshopModExecuteConsumer _consumer;

    public WorkshopModExecuteConsumerTests()
    {
        _consumer = new WorkshopModExecuteConsumer(
            _installOperation.Object,
            _updateOperation.Object,
            _processingService.Object,
            _workshopModsContext.Object,
            _logger.Object
        );
    }

    [Fact]
    public async Task Consume_WhenInstallExecuteSucceeds_ShouldPublishCompleteWithFilesChanged()
    {
        _installOperation.Setup(x => x.ExecuteAsync("mod1", It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(OperationResult.Successful());

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

        await _consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("mod1");
        _installOperation.Verify(x => x.ExecuteAsync("mod1", It.IsAny<List<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenUpdateExecuteSucceeds_ShouldPublishCompleteWithUpdateStatus()
    {
        _updateOperation.Setup(x => x.ExecuteAsync("mod1", It.IsAny<List<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Successful());

        var context = TestHelpers.CreateContext(
            new WorkshopModExecuteCommand
            {
                WorkshopModId = "mod1",
                OperationType = WorkshopModOperationType.Update,
                SelectedPbos = ["a.pbo"]
            }
        );
        WorkshopModExecuteComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModExecuteComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModExecuteComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await _consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("mod1");
        _updateOperation.Verify(x => x.ExecuteAsync("mod1", It.IsAny<List<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenExecuteFails_ShouldPublishFaulted()
    {
        _installOperation.Setup(x => x.ExecuteAsync("mod1", It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(OperationResult.Failure("mod1 not found"));

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

        await _consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Contain("not found");
        published.FaultedState.Should().Be("Executing");
    }
}

public class WorkshopModUninstallConsumerTests
{
    private readonly Mock<IWorkshopModsContext> _workshopModsContext = new();
    private readonly Mock<IWorkshopModsProcessingService> _processingService = new();
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly Mock<IUninstallOperation> _uninstallOperation = new();
    private readonly WorkshopModUninstallConsumer _consumer;

    public WorkshopModUninstallConsumerTests()
    {
        _consumer = new WorkshopModUninstallConsumer(_uninstallOperation.Object, _processingService.Object, _workshopModsContext.Object, _logger.Object);
    }

    [Fact]
    public async Task Consume_WhenUninstallSucceeds_ShouldPublishCompleteWithFilesChanged()
    {
        _uninstallOperation.Setup(x => x.ExecuteAsync("mod1", It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(OperationResult.Successful());

        var context = TestHelpers.CreateContext(new WorkshopModUninstallInternalCommand { WorkshopModId = "mod1" });
        WorkshopModUninstallComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModUninstallComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModUninstallComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await _consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("mod1");
        published.FilesChanged.Should().BeTrue();
    }

    [Fact]
    public async Task Consume_WhenUninstallSucceeds_WithNoFilesChanged_ShouldPublishFilesChangedFalse()
    {
        _uninstallOperation.Setup(x => x.ExecuteAsync("mod1", It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(OperationResult.Successful(filesChanged: false));

        var context = TestHelpers.CreateContext(new WorkshopModUninstallInternalCommand { WorkshopModId = "mod1" });
        WorkshopModUninstallComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModUninstallComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModUninstallComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await _consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.FilesChanged.Should().BeFalse();
    }

    [Fact]
    public async Task Consume_WhenUninstallFails_ShouldPublishFaulted()
    {
        _uninstallOperation.Setup(x => x.ExecuteAsync("mod1", It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(OperationResult.Failure("mod1 not found"));

        var context = TestHelpers.CreateContext(new WorkshopModUninstallInternalCommand { WorkshopModId = "mod1" });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await _consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.FaultedState.Should().Be("Uninstalling");
        published.ErrorMessage.Should().Contain("not found");
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
