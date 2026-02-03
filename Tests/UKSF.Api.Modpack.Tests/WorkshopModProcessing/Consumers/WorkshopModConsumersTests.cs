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

public class WorkshopModInstallConsumerTests
{
    [Fact]
    public async Task Consume_WhenInstallSucceeds_ShouldPublishComplete()
    {
        var operation = new Mock<IInstallOperation>();
        operation.Setup(x => x.InstallAsync("mod1", It.IsAny<List<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(InstallResult.Successful());
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModInstallConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModInstallInternalCommand { WorkshopModId = "mod1", SelectedPbos = ["a.pbo"] });
        WorkshopModInstallComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModInstallComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModInstallComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("mod1");
        published.FilesChanged.Should().BeTrue();
    }

    [Fact]
    public async Task Consume_WhenInstallFails_ShouldPublishFaulted()
    {
        var operation = new Mock<IInstallOperation>();
        operation.Setup(x => x.InstallAsync("mod1", It.IsAny<List<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(InstallResult.Failure("failed"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModInstallConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModInstallInternalCommand { WorkshopModId = "mod1", SelectedPbos = ["a.pbo"] });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("failed");
        published.FaultedState.Should().Be("Installing");
    }

    [Fact]
    public async Task Consume_WhenInstallCancelled_ShouldPublishFaulted()
    {
        var operation = new Mock<IInstallOperation>();
        operation.Setup(x => x.InstallAsync("mod1", It.IsAny<List<string>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModInstallConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModInstallInternalCommand { WorkshopModId = "mod1", SelectedPbos = ["a.pbo"] });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("Operation cancelled");
        published.FaultedState.Should().Be("Installing");
    }

    [Fact]
    public async Task Consume_WhenInstallThrows_ShouldPublishFaulted()
    {
        var operation = new Mock<IInstallOperation>();
        operation.Setup(x => x.InstallAsync("mod1", It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("boom"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModInstallConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModInstallInternalCommand { WorkshopModId = "mod1", SelectedPbos = ["a.pbo"] });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("boom");
        published.FaultedState.Should().Be("Installing");
    }

    private static Mock<ConsumeContext<TMessage>> CreateContext<TMessage>(TMessage message) where TMessage : class
    {
        var context = new Mock<ConsumeContext<TMessage>>();
        context.SetupGet(x => x.Message).Returns(message);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return context;
    }
}

public class WorkshopModInstallCheckConsumerTests
{
    [Fact]
    public async Task Consume_WhenCheckSucceeds_ShouldPublishCheckComplete()
    {
        var operation = new Mock<IInstallOperation>();
        operation.Setup(x => x.CheckAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(CheckResult.Successful(true));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModInstallCheckConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModInstallCheckCommand { WorkshopModId = "mod1" });
        WorkshopModInstallCheckComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModInstallCheckComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModInstallCheckComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.InterventionRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Consume_WhenCheckFails_ShouldPublishFaulted()
    {
        var operation = new Mock<IInstallOperation>();
        operation.Setup(x => x.CheckAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(CheckResult.Failure("bad"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModInstallCheckConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModInstallCheckCommand { WorkshopModId = "mod1" });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.FaultedState.Should().Be("InstallingChecking");
        published.ErrorMessage.Should().Be("bad");
    }

    [Fact]
    public async Task Consume_WhenCheckThrows_ShouldPublishFaulted()
    {
        var operation = new Mock<IInstallOperation>();
        operation.Setup(x => x.CheckAsync("mod1", It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModInstallCheckConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModInstallCheckCommand { WorkshopModId = "mod1" });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("boom");
        published.FaultedState.Should().Be("InstallingChecking");
    }

    private static Mock<ConsumeContext<TMessage>> CreateContext<TMessage>(TMessage message) where TMessage : class
    {
        var context = new Mock<ConsumeContext<TMessage>>();
        context.SetupGet(x => x.Message).Returns(message);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return context;
    }
}

public class WorkshopModInstallDownloadConsumerTests
{
    [Fact]
    public async Task Consume_WhenDownloadSucceeds_ShouldPublishComplete()
    {
        var operation = new Mock<IInstallOperation>();
        operation.Setup(x => x.DownloadAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(DownloadResult.Successful());
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModInstallDownloadConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModInstallDownloadCommand { WorkshopModId = "mod1" });
        WorkshopModInstallDownloadComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModInstallDownloadComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModInstallDownloadComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("mod1");
    }

    [Fact]
    public async Task Consume_WhenDownloadFails_ShouldPublishFaulted()
    {
        var operation = new Mock<IInstallOperation>();
        operation.Setup(x => x.DownloadAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(DownloadResult.Failure("bad"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModInstallDownloadConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModInstallDownloadCommand { WorkshopModId = "mod1" });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.FaultedState.Should().Be("InstallingDownloading");
        published.ErrorMessage.Should().Be("bad");
    }

    [Fact]
    public async Task Consume_WhenDownloadCancelled_ShouldPublishFaulted()
    {
        var operation = new Mock<IInstallOperation>();
        operation.Setup(x => x.DownloadAsync("mod1", It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModInstallDownloadConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModInstallDownloadCommand { WorkshopModId = "mod1" });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("Operation cancelled");
        published.FaultedState.Should().Be("InstallingDownloading");
    }

    [Fact]
    public async Task Consume_WhenDownloadThrows_ShouldPublishFaulted()
    {
        var operation = new Mock<IInstallOperation>();
        operation.Setup(x => x.DownloadAsync("mod1", It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModInstallDownloadConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModInstallDownloadCommand { WorkshopModId = "mod1" });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("boom");
        published.FaultedState.Should().Be("InstallingDownloading");
    }

    private static Mock<ConsumeContext<TMessage>> CreateContext<TMessage>(TMessage message) where TMessage : class
    {
        var context = new Mock<ConsumeContext<TMessage>>();
        context.SetupGet(x => x.Message).Returns(message);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return context;
    }
}

public class WorkshopModUpdateConsumerTests
{
    [Fact]
    public async Task Consume_WhenUpdateSucceeds_ShouldPublishComplete()
    {
        var operation = new Mock<IUpdateOperation>();
        operation.Setup(x => x.UpdateAsync("mod1", It.IsAny<List<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(UpdateResult.Successful());
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUpdateConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModUpdateInternalCommand { WorkshopModId = "mod1", SelectedPbos = ["a.pbo"] });
        WorkshopModUpdateComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModUpdateComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModUpdateComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("mod1");
        published.FilesChanged.Should().BeTrue();
    }

    [Fact]
    public async Task Consume_WhenUpdateFails_ShouldPublishFaulted()
    {
        var operation = new Mock<IUpdateOperation>();
        operation.Setup(x => x.UpdateAsync("mod1", It.IsAny<List<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(UpdateResult.Failure("bad"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUpdateConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModUpdateInternalCommand { WorkshopModId = "mod1", SelectedPbos = ["a.pbo"] });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.FaultedState.Should().Be("Updating");
        published.ErrorMessage.Should().Be("bad");
    }

    [Fact]
    public async Task Consume_WhenUpdateCancelled_ShouldPublishFaulted()
    {
        var operation = new Mock<IUpdateOperation>();
        operation.Setup(x => x.UpdateAsync("mod1", It.IsAny<List<string>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUpdateConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModUpdateInternalCommand { WorkshopModId = "mod1", SelectedPbos = ["a.pbo"] });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("Operation cancelled");
        published.FaultedState.Should().Be("Updating");
    }

    [Fact]
    public async Task Consume_WhenUpdateThrows_ShouldPublishFaulted()
    {
        var operation = new Mock<IUpdateOperation>();
        operation.Setup(x => x.UpdateAsync("mod1", It.IsAny<List<string>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUpdateConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModUpdateInternalCommand { WorkshopModId = "mod1", SelectedPbos = ["a.pbo"] });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("boom");
        published.FaultedState.Should().Be("Updating");
    }

    private static Mock<ConsumeContext<TMessage>> CreateContext<TMessage>(TMessage message) where TMessage : class
    {
        var context = new Mock<ConsumeContext<TMessage>>();
        context.SetupGet(x => x.Message).Returns(message);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return context;
    }
}

public class WorkshopModUpdateCheckConsumerTests
{
    [Fact]
    public async Task Consume_WhenPbosUnchanged_ShouldNotRequireIntervention()
    {
        var operation = new Mock<IUpdateOperation>();
        operation.Setup(x => x.CheckAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(CheckResult.Successful(false));

        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUpdateCheckConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModUpdateCheckCommand { WorkshopModId = "mod1" });
        WorkshopModUpdateCheckComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModUpdateCheckComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModUpdateCheckComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.InterventionRequired.Should().BeFalse();
    }

    [Fact]
    public async Task Consume_WhenPbosChanged_ShouldRequireIntervention()
    {
        var operation = new Mock<IUpdateOperation>();
        operation.Setup(x => x.CheckAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(CheckResult.Successful(true));

        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUpdateCheckConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModUpdateCheckCommand { WorkshopModId = "mod1" });
        WorkshopModUpdateCheckComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModUpdateCheckComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModUpdateCheckComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.InterventionRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Consume_WhenCheckFails_ShouldPublishFaulted()
    {
        var operation = new Mock<IUpdateOperation>();
        operation.Setup(x => x.CheckAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(CheckResult.Failure("bad"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUpdateCheckConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModUpdateCheckCommand { WorkshopModId = "mod1" });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("bad");
        published.FaultedState.Should().Be("UpdatingChecking");
    }

    [Fact]
    public async Task Consume_WhenCheckThrows_ShouldPublishFaulted()
    {
        var operation = new Mock<IUpdateOperation>();
        operation.Setup(x => x.CheckAsync("mod1", It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUpdateCheckConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModUpdateCheckCommand { WorkshopModId = "mod1" });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("boom");
        published.FaultedState.Should().Be("UpdatingChecking");
    }

    private static Mock<ConsumeContext<TMessage>> CreateContext<TMessage>(TMessage message) where TMessage : class
    {
        var context = new Mock<ConsumeContext<TMessage>>();
        context.SetupGet(x => x.Message).Returns(message);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return context;
    }
}

public class WorkshopModUpdateDownloadConsumerTests
{
    [Fact]
    public async Task Consume_WhenDownloadSucceeds_ShouldPublishComplete()
    {
        var operation = new Mock<IUpdateOperation>();
        operation.Setup(x => x.DownloadAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(DownloadResult.Successful());
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUpdateDownloadConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModUpdateDownloadCommand { WorkshopModId = "mod1" });
        WorkshopModUpdateDownloadComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModUpdateDownloadComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModUpdateDownloadComplete, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("mod1");
    }

    [Fact]
    public async Task Consume_WhenDownloadFails_ShouldPublishFaulted()
    {
        var operation = new Mock<IUpdateOperation>();
        operation.Setup(x => x.DownloadAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(DownloadResult.Failure("bad"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUpdateDownloadConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModUpdateDownloadCommand { WorkshopModId = "mod1" });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.FaultedState.Should().Be("UpdatingDownloading");
        published.ErrorMessage.Should().Be("bad");
    }

    [Fact]
    public async Task Consume_WhenDownloadCancelled_ShouldPublishFaulted()
    {
        var operation = new Mock<IUpdateOperation>();
        operation.Setup(x => x.DownloadAsync("mod1", It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUpdateDownloadConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModUpdateDownloadCommand { WorkshopModId = "mod1" });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("Operation cancelled");
        published.FaultedState.Should().Be("UpdatingDownloading");
    }

    [Fact]
    public async Task Consume_WhenDownloadThrows_ShouldPublishFaulted()
    {
        var operation = new Mock<IUpdateOperation>();
        operation.Setup(x => x.DownloadAsync("mod1", It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUpdateDownloadConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModUpdateDownloadCommand { WorkshopModId = "mod1" });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("boom");
        published.FaultedState.Should().Be("UpdatingDownloading");
    }

    private static Mock<ConsumeContext<TMessage>> CreateContext<TMessage>(TMessage message) where TMessage : class
    {
        var context = new Mock<ConsumeContext<TMessage>>();
        context.SetupGet(x => x.Message).Returns(message);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return context;
    }
}

public class WorkshopModUninstallConsumerTests
{
    [Fact]
    public async Task Consume_WhenUninstallSucceeds_ShouldPublishComplete()
    {
        var operation = new Mock<IUninstallOperation>();
        operation.Setup(x => x.UninstallAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(UninstallResult.Successful(true));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUninstallConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModUninstallInternalCommand { WorkshopModId = "mod1" });
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
        operation.Setup(x => x.UninstallAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(UninstallResult.Successful(false));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUninstallConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModUninstallInternalCommand { WorkshopModId = "mod1" });
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
        operation.Setup(x => x.UninstallAsync("mod1", It.IsAny<CancellationToken>())).ReturnsAsync(UninstallResult.Failure("bad"));
        Mock<IUksfLogger> logger = new();
        var consumer = new WorkshopModUninstallConsumer(operation.Object, logger.Object);

        var context = CreateContext(new WorkshopModUninstallInternalCommand { WorkshopModId = "mod1" });
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

        var context = CreateContext(new WorkshopModUninstallInternalCommand { WorkshopModId = "mod1" });
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

        var context = CreateContext(new WorkshopModUninstallInternalCommand { WorkshopModId = "mod1" });
        WorkshopModOperationFaulted published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((msg, _) => published = msg)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.Should().NotBeNull();
        published!.ErrorMessage.Should().Be("boom");
        published.FaultedState.Should().Be("Uninstalling");
    }

    private static Mock<ConsumeContext<TMessage>> CreateContext<TMessage>(TMessage message) where TMessage : class
    {
        var context = new Mock<ConsumeContext<TMessage>>();
        context.SetupGet(x => x.Message).Returns(message);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return context;
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

        var consumeContext = CreateContext(new WorkshopModCleanupCommand { WorkshopModId = "mod1" });
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

        var consumeContext = CreateContext(new WorkshopModCleanupCommand { WorkshopModId = "mod1", FilesChanged = true });
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

        var consumeContext = CreateContext(new WorkshopModCleanupCommand { WorkshopModId = "mod1", FilesChanged = true });
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

        var consumeContext = CreateContext(new WorkshopModCleanupCommand { WorkshopModId = "mod1", FilesChanged = true });
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

        var consumeContext = CreateContext(new WorkshopModCleanupCommand { WorkshopModId = "mod1", FilesChanged = false });
        consumeContext.Setup(x => x.Publish(It.IsAny<WorkshopModCleanupComplete>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await consumer.Consume(consumeContext.Object);

        processingService.Verify(x => x.QueueDevBuild(), Times.Never);
    }

    private static Mock<ConsumeContext<TMessage>> CreateContext<TMessage>(TMessage message) where TMessage : class
    {
        var context = new Mock<ConsumeContext<TMessage>>();
        context.SetupGet(x => x.Message).Returns(message);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return context;
    }
}
