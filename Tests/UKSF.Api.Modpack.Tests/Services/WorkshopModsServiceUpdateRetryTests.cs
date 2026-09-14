using FluentAssertions;
using MassTransit;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing;
using Xunit;

namespace UKSF.Api.Modpack.Tests.Services;

public class WorkshopModsServiceUpdateRetryTests
{
    private readonly Mock<IWorkshopModsContext> _context = new();
    private readonly Mock<ISteamApiService> _steamApiService = new();
    private readonly Mock<IPublishEndpoint> _publishEndpoint = new();
    private readonly WorkshopModsService _subject;

    public WorkshopModsServiceUpdateRetryTests()
    {
        Mock<IUksfLogger> mockLogger = new();
        _subject = new WorkshopModsService(_context.Object, _steamApiService.Object, _publishEndpoint.Object, mockLogger.Object);
    }

    [Fact]
    public async Task UpdateWorkshopMod_WhenMissing_ShouldThrowNotFound()
    {
        _context.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>())).Returns((DomainWorkshopMod)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _subject.UpdateWorkshopMod("missing"));
    }

    [Fact]
    public async Task UpdateWorkshopMod_WhenAlreadyUpdating_ShouldThrowBadRequest()
    {
        var workshopMod = new DomainWorkshopMod
        {
            Name = "Test",
            Status = WorkshopModStatus.Updating,
            SteamId = "steam-id"
        };
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);

        await Assert.ThrowsAsync<BadRequestException>(() => _subject.UpdateWorkshopMod("steam-id"));
    }

    [Fact]
    public async Task UpdateWorkshopMod_WhenInterventionRequired_ShouldThrowBadRequest()
    {
        var workshopMod = new DomainWorkshopMod
        {
            Name = "Test",
            Status = WorkshopModStatus.InterventionRequired,
            SteamId = "steam-id"
        };
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);

        await Assert.ThrowsAsync<BadRequestException>(() => _subject.UpdateWorkshopMod("steam-id"));
    }

    [Fact]
    public async Task UpdateWorkshopMod_WhenNoUpdateAvailable_ShouldThrowBadRequest()
    {
        var workshopMod = new DomainWorkshopMod
        {
            Id = "mod-id",
            Name = "Test",
            SteamId = "steam-id",
            Status = WorkshopModStatus.Installed,
            LastUpdatedLocally = DateTime.UtcNow
        };
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _steamApiService.Setup(x => x.GetWorkshopModInfo("steam-id"))
                        .ReturnsAsync(new WorkshopModInfo { Name = "Test", UpdatedDate = workshopMod.LastUpdatedLocally });

        await Assert.ThrowsAsync<BadRequestException>(() => _subject.UpdateWorkshopMod("steam-id"));
    }

    [Fact]
    public async Task UpdateWorkshopMod_WhenUpdateAvailable_ShouldReplaceAndPublish()
    {
        var workshopMod = new DomainWorkshopMod
        {
            Id = "mod-id",
            Name = "Test",
            SteamId = "steam-id",
            Status = WorkshopModStatus.Installed,
            LastUpdatedLocally = DateTime.UtcNow.AddDays(-1)
        };
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _steamApiService.Setup(x => x.GetWorkshopModInfo("steam-id")).ReturnsAsync(new WorkshopModInfo { Name = "Test", UpdatedDate = DateTime.UtcNow });
        _context.Setup(x => x.Replace(workshopMod)).Returns(Task.CompletedTask);

        WorkshopModUpdateCommand published = null;
        _publishEndpoint.Setup(x => x.Publish(It.IsAny<WorkshopModUpdateCommand>(), It.IsAny<CancellationToken>()))
                        .Callback<WorkshopModUpdateCommand, CancellationToken>((msg, _) => published = msg)
                        .Returns(Task.CompletedTask);

        await _subject.UpdateWorkshopMod("steam-id");

        workshopMod.Status.Should().Be(WorkshopModStatus.Updating);
        workshopMod.StatusMessage.Should().Be("Preparing to update...");
        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("steam-id");
    }

    [Fact]
    public async Task UpdateWorkshopMod_WhenUpdateAvailable_ShouldRecordLastOperation()
    {
        var workshopMod = new DomainWorkshopMod
        {
            Id = "mod-id",
            Name = "Test",
            SteamId = "steam-id",
            Status = WorkshopModStatus.Installed,
            LastUpdatedLocally = DateTime.UtcNow.AddDays(-1)
        };
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _steamApiService.Setup(x => x.GetWorkshopModInfo("steam-id")).ReturnsAsync(new WorkshopModInfo { Name = "Test", UpdatedDate = DateTime.UtcNow });
        _context.Setup(x => x.Replace(workshopMod)).Returns(Task.CompletedTask);

        await _subject.UpdateWorkshopMod("steam-id");

        workshopMod.LastOperation.Should().Be(WorkshopModOperationType.Update);
    }

    [Fact]
    public async Task RetryWorkshopMod_WhenMissing_ShouldThrowNotFound()
    {
        _context.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>())).Returns((DomainWorkshopMod)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _subject.RetryWorkshopMod("missing"));
    }

    [Fact]
    public async Task RetryWorkshopMod_WhenNotErrored_ShouldThrowBadRequest()
    {
        var workshopMod = new DomainWorkshopMod
        {
            Name = "Test",
            SteamId = "steam-id",
            Status = WorkshopModStatus.Installed
        };
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);

        await Assert.ThrowsAsync<BadRequestException>(() => _subject.RetryWorkshopMod("steam-id"));

        _context.Verify(x => x.Replace(It.IsAny<DomainWorkshopMod>()), Times.Never);
        _publishEndpoint.Verify(x => x.Publish(It.IsAny<WorkshopModUpdateCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetryWorkshopMod_WhenErroredUpdate_ShouldResetAndPublishUpdate()
    {
        var workshopMod = new DomainWorkshopMod
        {
            Id = "mod-id",
            Name = "Test",
            SteamId = "steam-id",
            Status = WorkshopModStatus.Error,
            ErrorMessage = "boom",
            LastOperation = WorkshopModOperationType.Update
        };
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _context.Setup(x => x.Replace(workshopMod)).Returns(Task.CompletedTask);

        WorkshopModUpdateCommand published = null;
        _publishEndpoint.Setup(x => x.Publish(It.IsAny<WorkshopModUpdateCommand>(), It.IsAny<CancellationToken>()))
                        .Callback<WorkshopModUpdateCommand, CancellationToken>((msg, _) => published = msg)
                        .Returns(Task.CompletedTask);

        await _subject.RetryWorkshopMod("steam-id");

        workshopMod.Status.Should().Be(WorkshopModStatus.Updating);
        workshopMod.ErrorMessage.Should().BeNull();
        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("steam-id");
    }

    [Fact]
    public async Task RetryWorkshopMod_WhenErroredInstall_ShouldPublishInstall()
    {
        var workshopMod = new DomainWorkshopMod
        {
            Id = "mod-id",
            Name = "Test",
            SteamId = "steam-id",
            Status = WorkshopModStatus.Error,
            LastOperation = WorkshopModOperationType.Install
        };
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _context.Setup(x => x.Replace(workshopMod)).Returns(Task.CompletedTask);

        WorkshopModInstallCommand published = null;
        _publishEndpoint.Setup(x => x.Publish(It.IsAny<WorkshopModInstallCommand>(), It.IsAny<CancellationToken>()))
                        .Callback<WorkshopModInstallCommand, CancellationToken>((msg, _) => published = msg)
                        .Returns(Task.CompletedTask);

        await _subject.RetryWorkshopMod("steam-id");

        workshopMod.Status.Should().Be(WorkshopModStatus.Installing);
        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("steam-id");
    }

    [Fact]
    public async Task RetryWorkshopMod_WhenErroredUninstall_ShouldPublishUninstall()
    {
        var workshopMod = new DomainWorkshopMod
        {
            Id = "mod-id",
            Name = "Test",
            SteamId = "steam-id",
            Status = WorkshopModStatus.Error,
            LastOperation = WorkshopModOperationType.Uninstall
        };
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _context.Setup(x => x.Replace(workshopMod)).Returns(Task.CompletedTask);

        WorkshopModUninstallCommand published = null;
        _publishEndpoint.Setup(x => x.Publish(It.IsAny<WorkshopModUninstallCommand>(), It.IsAny<CancellationToken>()))
                        .Callback<WorkshopModUninstallCommand, CancellationToken>((msg, _) => published = msg)
                        .Returns(Task.CompletedTask);

        await _subject.RetryWorkshopMod("steam-id");

        workshopMod.Status.Should().Be(WorkshopModStatus.Uninstalling);
        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("steam-id");
    }

    [Fact]
    public async Task RetryWorkshopMod_WhenNoLastOperation_ShouldDefaultToUpdate()
    {
        var workshopMod = new DomainWorkshopMod
        {
            Id = "mod-id",
            Name = "Test",
            SteamId = "steam-id",
            Status = WorkshopModStatus.Error,
            LastOperation = null
        };
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _context.Setup(x => x.Replace(workshopMod)).Returns(Task.CompletedTask);

        WorkshopModUpdateCommand published = null;
        _publishEndpoint.Setup(x => x.Publish(It.IsAny<WorkshopModUpdateCommand>(), It.IsAny<CancellationToken>()))
                        .Callback<WorkshopModUpdateCommand, CancellationToken>((msg, _) => published = msg)
                        .Returns(Task.CompletedTask);

        await _subject.RetryWorkshopMod("steam-id");

        workshopMod.Status.Should().Be(WorkshopModStatus.Updating);
        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("steam-id");
    }
}
