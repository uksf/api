using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing;
using UKSF.Api.Modpack.WorkshopModProcessing.Consumers;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;
using Xunit;

namespace UKSF.Api.Modpack.Tests.WorkshopModProcessing;

// A mod uninstalled before it was ever released is deleted from the database by the uninstall itself, so the dev build change
// must be named from what the uninstall read before deleting it.
public class WorkshopModUninstallNamingTests
{
    [Fact]
    public async Task UninstallConsumer_ShouldPublishTheModNameReadBeforeTheUninstall()
    {
        var workshopModsContext = new Mock<IWorkshopModsContext>();
        var workshopMod = new DomainWorkshopMod { SteamId = "mod1", Name = "3CB BAF Weapons" };
        workshopModsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>())).Returns(workshopMod);
        var uninstallOperation = new Mock<IUninstallOperation>();
        uninstallOperation.Setup(x => x.ExecuteAsync("mod1", It.IsAny<List<string>>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                          .Callback(() => workshopModsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>()))
                                                             .Returns((DomainWorkshopMod)null)
                          )
                          .ReturnsAsync(OperationResult.Successful());
        var consumer = new WorkshopModUninstallConsumer(
            uninstallOperation.Object,
            new Mock<IWorkshopModsProcessingService>().Object,
            workshopModsContext.Object,
            new Mock<IUksfLogger>().Object
        );
        var context = new Mock<ConsumeContext<WorkshopModUninstallInternalCommand>>();
        context.SetupGet(x => x.Message).Returns(new WorkshopModUninstallInternalCommand { WorkshopModId = "mod1" });
        WorkshopModUninstallComplete published = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModUninstallComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModUninstallComplete, CancellationToken>((message, _) => published = message)
               .Returns(Task.CompletedTask);

        await consumer.Consume(context.Object);

        published.WorkshopModName.Should().Be("3CB BAF Weapons");
    }

    [Fact]
    public async Task StateMachine_ShouldPassTheUninstalledModNameToCleanup()
    {
        await using var provider = new ServiceCollection()
                                   .AddMassTransitTestHarness(x => x.AddSagaStateMachine<WorkshopModStateMachine, WorkshopModInstanceState>())
                                   .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new WorkshopModUninstallCommand { WorkshopModId = "mod1" });
        (await harness.Published.Any<WorkshopModUninstallInternalCommand>()).Should().BeTrue();
        await harness.Bus.Publish(
            new WorkshopModUninstallComplete
            {
                WorkshopModId = "mod1",
                FilesChanged = true,
                WorkshopModName = "3CB BAF Weapons"
            }
        );

        (await harness.Published.Any<WorkshopModCleanupCommand>()).Should().BeTrue();
        var cleanup = harness.Published.Select<WorkshopModCleanupCommand>().Single().Context.Message;
        cleanup.WorkshopModName.Should().Be("3CB BAF Weapons");
        cleanup.FilesChanged.Should().BeTrue();
    }
}
