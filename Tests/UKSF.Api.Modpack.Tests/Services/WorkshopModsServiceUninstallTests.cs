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

public class WorkshopModsServiceUninstallTests
{
    private readonly Mock<IWorkshopModsContext> _context = new();
    private readonly Mock<ISteamApiService> _steamApiService = new();
    private readonly Mock<IPublishEndpoint> _publishEndpoint = new();
    private readonly WorkshopModsService _subject;

    public WorkshopModsServiceUninstallTests()
    {
        Mock<IUksfLogger> mockLogger = new();
        _subject = new WorkshopModsService(_context.Object, _steamApiService.Object, _publishEndpoint.Object, mockLogger.Object);
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
    public async Task UninstallWorkshopMod_WhenExtensionFileSharedWithAnotherMod_ShouldThrowBadRequest()
    {
        var workshopMod = new DomainWorkshopMod
        {
            Id = "mod-id",
            Name = "Test",
            Status = WorkshopModStatus.Installed,
            Extensions = ["Shared_Extension.dll"],
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
                    Extensions = ["shared_extension.dll"],
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

        await Assert.ThrowsAsync<NotFoundException>(() => _subject.ResolveWorkshopModManualIntervention("missing", ["a"], []));
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

        await Assert.ThrowsAsync<BadRequestException>(() => _subject.ResolveWorkshopModManualIntervention("steam-id", ["a"], []));
    }

    [Fact]
    public async Task ResolveWorkshopModManualIntervention_WhenNothingSelected_ShouldThrowBadRequest()
    {
        var workshopMod = new DomainWorkshopMod
        {
            Name = "Test",
            Status = WorkshopModStatus.InterventionRequired,
            SteamId = "steam-id"
        };
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);

        await Assert.ThrowsAsync<BadRequestException>(() => _subject.ResolveWorkshopModManualIntervention("steam-id", null, null));
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

    [Fact]
    public void GetPendingReleaseMods_ShouldReturnOnlyPendingStatuses()
    {
        var mods = new List<DomainWorkshopMod>
        {
            new()
            {
                Name = "Added Mod",
                SteamId = "1",
                Status = WorkshopModStatus.InstalledPendingRelease
            },
            new()
            {
                Name = "Updated Mod",
                SteamId = "2",
                Status = WorkshopModStatus.UpdatedPendingRelease
            },
            new()
            {
                Name = "Removed Mod",
                SteamId = "3",
                Status = WorkshopModStatus.UninstalledPendingRelease
            },
            new()
            {
                Name = "Installed Mod",
                SteamId = "4",
                Status = WorkshopModStatus.Installed
            },
            new()
            {
                Name = "Installing Mod",
                SteamId = "5",
                Status = WorkshopModStatus.Installing
            },
            new()
            {
                Name = "Error Mod",
                SteamId = "6",
                Status = WorkshopModStatus.Error
            }
        };
        _context.Setup(x => x.Get()).Returns(mods);

        var result = _subject.GetPendingReleaseMods();

        result.Should().HaveCount(3);
        result.Select(m => m.Name).Should().BeEquivalentTo(["Added Mod", "Updated Mod", "Removed Mod"]);
    }
}
