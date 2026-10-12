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

public class WorkshopModResumedConsumersTests
{
    private readonly Mock<IWorkshopModsContext> _workshopModsContext = new();
    private readonly Mock<IWorkshopModsProcessingService> _processingService = new();
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly Mock<IInstallOperation> _installOperation = new();
    private readonly Mock<IUpdateOperation> _updateOperation = new();
    private readonly Mock<IUninstallOperation> _uninstallOperation = new();

    private static (Mock<ConsumeContext<TMessage>> Context, List<object> Published) CreateContext<TMessage>(TMessage message) where TMessage : class
    {
        var context = new Mock<ConsumeContext<TMessage>>();
        var published = new List<object>();
        context.SetupGet(x => x.Message).Returns(message);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        context.Setup(x => x.Publish(It.IsAny<WorkshopModExecuteComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModExecuteComplete, CancellationToken>((x, _) => published.Add(x))
               .Returns(Task.CompletedTask);
        context.Setup(x => x.Publish(It.IsAny<WorkshopModUninstallComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModUninstallComplete, CancellationToken>((x, _) => published.Add(x))
               .Returns(Task.CompletedTask);
        context.Setup(x => x.Publish(It.IsAny<WorkshopModOperationFaulted>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModOperationFaulted, CancellationToken>((x, _) => published.Add(x))
               .Returns(Task.CompletedTask);
        return (context, published);
    }

    private WorkshopModExecuteConsumer ExecuteConsumer() =>
        new(_installOperation.Object, _updateOperation.Object, _processingService.Object, _workshopModsContext.Object, _logger.Object);

    private WorkshopModUninstallConsumer UninstallConsumer() =>
        new(_uninstallOperation.Object, _processingService.Object, _workshopModsContext.Object, _logger.Object);

    private void GivenMod(string steamId, string name) =>
        _workshopModsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>()))
                            .Returns(new DomainWorkshopMod { SteamId = steamId, Name = name });

    [Fact]
    public async Task Execute_WhenResumedAndFilesAlreadyMatch_ReportsFilesChanged()
    {
        _updateOperation.Setup(x => x.ExecuteAsync("mod1", It.IsAny<List<string>>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(OperationResult.Successful(filesChanged: false));
        var (context, published) = CreateContext(
            new WorkshopModExecuteCommand
            {
                WorkshopModId = "mod1",
                OperationType = WorkshopModOperationType.Update,
                Resumed = true
            }
        );

        await ExecuteConsumer().Consume(context.Object);

        published.Should().ContainSingle().Which.Should().BeEquivalentTo(new WorkshopModExecuteComplete { WorkshopModId = "mod1", FilesChanged = true });
    }

    [Fact]
    public async Task Execute_WhenNotResumed_ReportsOperationFilesChanged()
    {
        _updateOperation.Setup(x => x.ExecuteAsync("mod1", It.IsAny<List<string>>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(OperationResult.Successful(filesChanged: false));
        var (context, published) = CreateContext(new WorkshopModExecuteCommand { WorkshopModId = "mod1", OperationType = WorkshopModOperationType.Update });

        await ExecuteConsumer().Consume(context.Object);

        published.Should().ContainSingle().Which.Should().BeEquivalentTo(new WorkshopModExecuteComplete { WorkshopModId = "mod1", FilesChanged = false });
    }

    [Fact]
    public async Task Uninstall_WhenResumedAndAlreadyUninstalled_ReportsFilesChanged()
    {
        GivenMod("mod1", "Mod One");
        _uninstallOperation.Setup(x => x.ExecuteAsync("mod1", It.IsAny<List<string>>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(OperationResult.Successful(filesChanged: false));
        var (context, published) = CreateContext(new WorkshopModUninstallInternalCommand { WorkshopModId = "mod1", Resumed = true });

        await UninstallConsumer().Consume(context.Object);

        published.Should()
        .ContainSingle()
        .Which.Should()
        .BeEquivalentTo(
            new WorkshopModUninstallComplete
            {
                WorkshopModId = "mod1",
                FilesChanged = true,
                WorkshopModName = "Mod One"
            }
        );
    }

    [Fact]
    public async Task Uninstall_WhenResumedAndModRowAlreadyDeleted_CompletesWithFilesChanged()
    {
        var (context, published) = CreateContext(new WorkshopModUninstallInternalCommand { WorkshopModId = "mod1", Resumed = true });

        await UninstallConsumer().Consume(context.Object);

        published.Should().ContainSingle().Which.Should().BeEquivalentTo(new WorkshopModUninstallComplete { WorkshopModId = "mod1", FilesChanged = true });
        _uninstallOperation.Verify(
            x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Uninstall_WhenNotResumedAndModMissing_Faults()
    {
        _uninstallOperation.Setup(x => x.ExecuteAsync("mod1", It.IsAny<List<string>>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(OperationResult.Failure("Workshop mod mod1 not found"));
        var (context, published) = CreateContext(new WorkshopModUninstallInternalCommand { WorkshopModId = "mod1" });

        await UninstallConsumer().Consume(context.Object);

        published.Should().ContainSingle().Which.Should().BeOfType<WorkshopModOperationFaulted>();
    }
}
