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
    public async Task GetWorkshopModUpdatedDates_ShouldReturnDatesForEveryTrackedMod()
    {
        var expected = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        _context.Setup(x => x.Get()).Returns([new DomainWorkshopMod { SteamId = "123" }, new DomainWorkshopMod { SteamId = "456" }]);
        _steamApiService.Setup(x => x.GetWorkshopModInfos(It.Is<IReadOnlyCollection<string>>(ids => ids.Contains("123") && ids.Contains("456"))))
                        .ReturnsAsync(new Dictionary<string, WorkshopModInfo> { ["123"] = new() { Name = "Test Mod", UpdatedDate = expected } });

        var result = await _subject.GetWorkshopModUpdatedDates();

        result.Should().ContainKey("123").WhoseValue.Should().Be(expected);
        result.Should().NotContainKey("456");
    }

    [Fact]
    public async Task InstallWorkshopMod_WhenAlreadyExists_ShouldThrowBadRequest()
    {
        var existing = new DomainWorkshopMod { SteamId = "123", Status = WorkshopModStatus.Installed };
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(existing)))).Returns(existing);

        await Assert.ThrowsAsync<BadRequestException>(() => _subject.InstallWorkshopMod("123", false));

        _context.Verify(x => x.Add(It.IsAny<DomainWorkshopMod>()), Times.Never);
        _context.Verify(x => x.Replace(It.IsAny<DomainWorkshopMod>()), Times.Never);
        _publishEndpoint.Verify(x => x.Publish(It.IsAny<WorkshopModInstallCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InstallWorkshopMod_WhenNoExisting_ShouldAddAndPublish()
    {
        var modInfo = new WorkshopModInfo { Name = "Test Mod", UpdatedDate = DateTime.UtcNow };
        _steamApiService.Setup(x => x.GetWorkshopModInfo("123")).ReturnsAsync(modInfo);
        _context.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>())).Returns((DomainWorkshopMod)null);

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

        await _subject.InstallWorkshopMod("123", true, "@FolderName");

        added.Should().NotBeNull();
        added!.SteamId.Should().Be("123");
        added.Name.Should().Be("Test Mod");
        added.RootMod.Should().BeTrue();
        added.FolderName.Should().Be("@FolderName");
        added.Status.Should().Be(WorkshopModStatus.Installing);
        _context.Verify(x => x.Replace(It.IsAny<DomainWorkshopMod>()), Times.Never);
        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("123");
    }

    [Fact]
    public async Task InstallWorkshopMod_WhenUninstalledExists_ShouldReuseAndPublish()
    {
        var existing = new DomainWorkshopMod
        {
            Id = "existing-id",
            SteamId = "123",
            Name = "Old Name",
            Status = WorkshopModStatus.Uninstalled,
            RootMod = false,
            FolderName = "@OldFolder",
            Pbos = ["old.pbo"],
            StatusMessage = "Uninstalled",
            ErrorMessage = "previous error"
        };
        var modInfo = new WorkshopModInfo { Name = "Fresh Name", UpdatedDate = DateTime.UtcNow };
        _steamApiService.Setup(x => x.GetWorkshopModInfo("123")).ReturnsAsync(modInfo);
        _context.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(existing)))).Returns(existing);

        DomainWorkshopMod replaced = null;
        _context.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Callback<DomainWorkshopMod>(mod => replaced = mod).Returns(Task.CompletedTask);

        WorkshopModInstallCommand published = null;
        _publishEndpoint.Setup(x => x.Publish(It.IsAny<WorkshopModInstallCommand>(), It.IsAny<CancellationToken>()))
                        .Callback<WorkshopModInstallCommand, CancellationToken>((msg, _) => published = msg)
                        .Returns(Task.CompletedTask);

        await _subject.InstallWorkshopMod("123", true, "@NewFolder");

        replaced.Should().NotBeNull();
        replaced!.Id.Should().Be("existing-id");
        replaced.SteamId.Should().Be("123");
        replaced.Name.Should().Be("Fresh Name");
        replaced.RootMod.Should().BeTrue();
        replaced.FolderName.Should().Be("@NewFolder");
        replaced.Status.Should().Be(WorkshopModStatus.Installing);
        replaced.Pbos.Should().BeEmpty();
        replaced.ErrorMessage.Should().BeNull();
        replaced.StatusMessage.Should().BeNull();
        _context.Verify(x => x.Add(It.IsAny<DomainWorkshopMod>()), Times.Never);
        published.Should().NotBeNull();
        published!.WorkshopModId.Should().Be("123");
    }
}
