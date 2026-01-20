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

public class WorkshopModsServiceTests
{
    private readonly Mock<IWorkshopModsContext> _context = new();
    private readonly Mock<ISteamApiService> _steamApiService = new();
    private readonly Mock<IPublishEndpoint> _publishEndpoint = new();
    private readonly WorkshopModsService _subject;

    public WorkshopModsServiceTests()
    {
        Mock<IUksfLogger> mockLogger = new();
        _subject = new WorkshopModsService(_context.Object, _steamApiService.Object, _publishEndpoint.Object, mockLogger.Object);
    }

    [Fact]
    public async Task GetWorkshopModUpdatedDate_ShouldReturnDateFromSteamApi()
    {
        var expected = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        _steamApiService.Setup(x => x.GetWorkshopModInfo("123")).ReturnsAsync(new WorkshopModInfo { Name = "Test Mod", UpdatedDate = expected });

        var result = await _subject.GetWorkshopModUpdatedDate("123");

        result.Should().Be(expected);
    }

    [Fact]
    public async Task InstallWorkshopMod_WhenAlreadyExists_ShouldThrowBadRequest()
    {
        _context.Setup(x => x.Get()).Returns(new List<DomainWorkshopMod> { new() { SteamId = "123", Status = WorkshopModStatus.Installed } });

        await Assert.ThrowsAsync<BadRequestException>(() => _subject.InstallWorkshopMod("123", false));
    }

    [Fact]
    public async Task InstallWorkshopMod_WhenUninstalledExists_ShouldAddAndPublish()
    {
        var modInfo = new WorkshopModInfo { Name = "Test Mod", UpdatedDate = DateTime.UtcNow };
        _steamApiService.Setup(x => x.GetWorkshopModInfo("123")).ReturnsAsync(modInfo);
        _context.Setup(x => x.Get()).Returns(new List<DomainWorkshopMod> { new() { SteamId = "123", Status = WorkshopModStatus.Uninstalled } });

        DomainWorkshopMod added = null;
        _context.Setup(x => x.Add(It.IsAny<DomainWorkshopMod>()))
                .Callback<DomainWorkshopMod>(mod =>
                    {
                        mod.Id = "new-id";
                        added = mod;
                    }
                )
                .Returns(Task.CompletedTask);

        WorkshopModInstallCommand published = null;
        _publishEndpoint.Setup(x => x.Publish(It.IsAny<WorkshopModInstallCommand>(), It.IsAny<CancellationToken>()))
                        .Callback<WorkshopModInstallCommand, CancellationToken>((msg, _) => published = msg)
                        .Returns(Task.CompletedTask);

        await _subject.InstallWorkshopMod("123", true);

        added.Should().NotBeNull();
        added!.SteamId.Should().Be("123");
        added.Name.Should().Be("Test Mod");
        added.RootMod.Should().BeTrue();
        added.Status.Should().Be(WorkshopModStatus.Installing);
        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("123");
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
    public async Task UninstallWorkshopMod_WhenMissing_ShouldThrowNotFound()
    {
        _context.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>())).Returns((DomainWorkshopMod)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _subject.UninstallWorkshopMod("missing"));
    }

    [Fact]
    public async Task UninstallWorkshopMod_WhenAlreadyUninstalled_ShouldThrowBadRequest()
    {
        var workshopMod = new DomainWorkshopMod
        {
            Name = "Test",
            Status = WorkshopModStatus.Uninstalled,
            SteamId = "steam-id"
        };
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);

        await Assert.ThrowsAsync<BadRequestException>(() => _subject.UninstallWorkshopMod("steam-id"));
    }

    [Fact]
    public async Task UninstallWorkshopMod_WhenConflictsExist_ShouldThrowBadRequest()
    {
        var workshopMod = new DomainWorkshopMod
        {
            Id = "mod-id",
            Name = "Test",
            Status = WorkshopModStatus.Installed,
            Pbos = ["Shared.PBO"],
            SteamId = "steam-id"
        };
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _context.Setup(x => x.Get())
        .Returns(
            new List<DomainWorkshopMod>
            {
                workshopMod,
                new()
                {
                    Id = "other-mod",
                    Status = WorkshopModStatus.Installed,
                    Pbos = ["shared.pbo"],
                    SteamId = "other-steam-id"
                }
            }
        );

        await Assert.ThrowsAsync<BadRequestException>(() => _subject.UninstallWorkshopMod("steam-id"));
    }

    [Fact]
    public async Task UninstallWorkshopMod_WhenValid_ShouldReplaceAndPublish()
    {
        var workshopMod = new DomainWorkshopMod
        {
            Id = "mod-id",
            Name = "Test",
            Status = WorkshopModStatus.Installed,
            Pbos = ["mod.pbo"],
            SteamId = "steam-id"
        };
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _context.Setup(x => x.Get()).Returns(new List<DomainWorkshopMod> { workshopMod });
        _context.Setup(x => x.Replace(workshopMod)).Returns(Task.CompletedTask);

        WorkshopModUninstallCommand published = null;
        _publishEndpoint.Setup(x => x.Publish(It.IsAny<WorkshopModUninstallCommand>(), It.IsAny<CancellationToken>()))
                        .Callback<WorkshopModUninstallCommand, CancellationToken>((msg, _) => published = msg)
                        .Returns(Task.CompletedTask);

        await _subject.UninstallWorkshopMod("steam-id");

        workshopMod.Status.Should().Be(WorkshopModStatus.Uninstalling);
        workshopMod.StatusMessage.Should().Be("Preparing to uninstall...");
        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("steam-id");
    }

    [Fact]
    public async Task ResolveWorkshopModManualIntervention_WhenMissing_ShouldThrowNotFound()
    {
        _context.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>())).Returns((DomainWorkshopMod)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _subject.ResolveWorkshopModManualIntervention("missing", ["a"]));
    }

    [Fact]
    public async Task ResolveWorkshopModManualIntervention_WhenNotRequired_ShouldThrowBadRequest()
    {
        var workshopMod = new DomainWorkshopMod
        {
            Name = "Test",
            Status = WorkshopModStatus.Installed,
            SteamId = "steam-id"
        };
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);

        await Assert.ThrowsAsync<BadRequestException>(() => _subject.ResolveWorkshopModManualIntervention("steam-id", ["a"]));
    }

    [Fact]
    public async Task ResolveWorkshopModManualIntervention_WhenSelectedNull_ShouldPublishEmptyList()
    {
        var workshopMod = new DomainWorkshopMod
        {
            Name = "Test",
            Status = WorkshopModStatus.InterventionRequired,
            SteamId = "steam-id"
        };
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);

        WorkshopModInterventionResolved published = null;
        _publishEndpoint.Setup(x => x.Publish(It.IsAny<WorkshopModInterventionResolved>(), It.IsAny<CancellationToken>()))
                        .Callback<WorkshopModInterventionResolved, CancellationToken>((msg, _) => published = msg)
                        .Returns(Task.CompletedTask);

        await _subject.ResolveWorkshopModManualIntervention("steam-id", null);

        published.Should().NotBeNull();
        published!.SelectedPbos.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteWorkshopMod_WhenMissing_ShouldThrowNotFound()
    {
        _context.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>())).Returns((DomainWorkshopMod)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _subject.DeleteWorkshopMod("missing"));
    }

    [Fact]
    public async Task DeleteWorkshopMod_WhenNotUninstalled_ShouldThrowBadRequest()
    {
        var workshopMod = new DomainWorkshopMod
        {
            Name = "Test",
            Status = WorkshopModStatus.Installed,
            SteamId = "steam-id"
        };
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);

        await Assert.ThrowsAsync<BadRequestException>(() => _subject.DeleteWorkshopMod("steam-id"));
    }

    [Fact]
    public async Task DeleteWorkshopMod_WhenUninstalled_ShouldDelete()
    {
        var workshopMod = new DomainWorkshopMod
        {
            Id = "mod-id",
            Name = "Test",
            Status = WorkshopModStatus.Uninstalled,
            SteamId = "steam-id"
        };
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _context.Setup(x => x.Delete(workshopMod)).Returns(Task.CompletedTask);

        await _subject.DeleteWorkshopMod("steam-id");

        _context.Verify(x => x.Delete(workshopMod), Times.Once);
    }
}
