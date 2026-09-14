using FluentAssertions;
using MassTransit;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing;
using UKSF.Api.Modpack.WorkshopModProcessing.Consumers;
using Xunit;

namespace UKSF.Api.Modpack.Tests.WorkshopModProcessing.Consumers;

public class WorkshopModCleanupConsumerTests
{
    private readonly Mock<IWorkshopModsProcessingService> _processingService = new();
    private readonly Mock<IWorkshopModsContext> _workshopModsContext = new();
    private readonly WorkshopModCleanupConsumer _subject;

    public WorkshopModCleanupConsumerTests()
    {
        _processingService.Setup(x => x.GetWorkshopModPath("mod1")).Returns("path");
        _subject = new WorkshopModCleanupConsumer(_processingService.Object, _workshopModsContext.Object, new Mock<IUksfLogger>().Object);
    }

    private DomainWorkshopMod SetupWorkshopMod(string name = "cTAB Connect", WorkshopModStatus status = WorkshopModStatus.InstalledPendingRelease)
    {
        var workshopMod = new DomainWorkshopMod
        {
            SteamId = "mod1",
            Name = name,
            Status = status
        };
        _workshopModsContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        return workshopMod;
    }

    private static Mock<ConsumeContext<WorkshopModCleanupCommand>> CreateContext(bool filesChanged, out Func<WorkshopModCleanupComplete> published)
    {
        var context = new Mock<ConsumeContext<WorkshopModCleanupCommand>>();
        context.SetupGet(x => x.Message).Returns(new WorkshopModCleanupCommand { WorkshopModId = "mod1", FilesChanged = filesChanged });
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);

        WorkshopModCleanupComplete complete = null;
        context.Setup(x => x.Publish(It.IsAny<WorkshopModCleanupComplete>(), It.IsAny<CancellationToken>()))
               .Callback<WorkshopModCleanupComplete, CancellationToken>((message, _) => complete = message)
               .Returns(Task.CompletedTask);

        published = () => complete;
        return context;
    }

    [Fact]
    public async Task Consume_WhenCleanupSucceeds_ShouldCleanFilesQueueBuildAndPublishComplete()
    {
        SetupWorkshopMod();
        var context = CreateContext(true, out var published);

        await _subject.Consume(context.Object);

        published().Should().NotBeNull();
        _processingService.Verify(x => x.CleanupWorkshopModFiles("path"), Times.Once);
        _processingService.Verify(x => x.QueueDevBuild("cTAB Connect", WorkshopModStatus.InstalledPendingRelease), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenWorkshopModAlreadyDeleted_ShouldStillQueueBuildNamingTheSteamId()
    {
        _workshopModsContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>())).Returns((DomainWorkshopMod)null);
        var context = CreateContext(true, out var published);

        await _subject.Consume(context.Object);

        published().Should().NotBeNull();
        _processingService.Verify(x => x.CleanupWorkshopModFiles("path"), Times.Once);
        _processingService.Verify(x => x.QueueDevBuild("Workshop mod mod1", WorkshopModStatus.Uninstalled), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenFilesChangedFalse_ShouldNotQueueDevBuild()
    {
        SetupWorkshopMod();
        var context = CreateContext(false, out var published);

        await _subject.Consume(context.Object);

        published().Should().NotBeNull();
        _processingService.Verify(x => x.QueueDevBuild(It.IsAny<string>(), It.IsAny<WorkshopModStatus>()), Times.Never);
    }

    [Fact]
    public async Task Consume_WhenCleanupThrows_ShouldStillPublishComplete()
    {
        SetupWorkshopMod();
        _processingService.Setup(x => x.CleanupWorkshopModFiles("path")).Throws(new IOException("fail"));
        var context = CreateContext(true, out var published);

        await _subject.Consume(context.Object);

        published().Should().NotBeNull();
    }
}
