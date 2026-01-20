using FluentAssertions;
using Moq;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Controllers;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Models.Request;
using UKSF.Api.Modpack.Services;
using Xunit;

namespace UKSF.Api.Modpack.Tests.Controllers;

public class WorkshopModsControllerTests
{
    private readonly Mock<IWorkshopModsContext> _context = new();
    private readonly Mock<IWorkshopModsService> _service = new();
    private readonly WorkshopModsController _subject;

    public WorkshopModsControllerTests()
    {
        _subject = new WorkshopModsController(_service.Object, _context.Object);
    }

    [Fact]
    public void GetWorkshopMods_ShouldReturnMappedResponses()
    {
        var updatedDate = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var workshopMod = new DomainWorkshopMod
        {
            Id = "mod-id",
            SteamId = "123",
            Name = "Test Mod",
            RootMod = true,
            Status = WorkshopModStatus.Installed,
            StatusMessage = "Installed",
            ErrorMessage = "None",
            LastUpdatedLocally = updatedDate,
            ModpackVersionFirstAdded = "1.0.0",
            ModpackVersionLastUpdated = "1.1.0",
            Pbos = ["a.pbo"],
            CustomFilesList = ["custom.txt"]
        };
        _context.Setup(x => x.Get()).Returns([workshopMod]);

        var result = _subject.GetWorkshopMods();

        result.Should().HaveCount(1);
        var mapped = result.Single();
        mapped.Id.Should().Be("mod-id");
        mapped.SteamId.Should().Be("123");
        mapped.Name.Should().Be("Test Mod");
        mapped.RootMod.Should().BeTrue();
        mapped.Status.Should().Be(nameof(WorkshopModStatus.Installed));
        mapped.StatusMessage.Should().Be("Installed");
        mapped.ErrorMessage.Should().Be("None");
        mapped.LastUpdatedLocally.Should().Be(updatedDate.ToString("o"));
        mapped.ModpackVersionFirstAdded.Should().Be("1.0.0");
        mapped.ModpackVersionLastUpdated.Should().Be("1.1.0");
        mapped.Pbos.Should().BeEquivalentTo("a.pbo");
        mapped.CustomFilesList.Should().BeEquivalentTo("custom.txt");
    }

    [Fact]
    public void GetWorkshopMod_WhenMissing_ShouldThrowNotFound()
    {
        _context.Setup(x => x.GetSingle("missing")).Returns((DomainWorkshopMod)null);

        Action action = () => _subject.GetWorkshopMod("missing");

        action.Should().Throw<NotFoundException>();
    }

    [Fact]
    public async Task GetWorkshopModUpdatedDate_ShouldReturnIsoString()
    {
        var updatedDate = new DateTime(2024, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        _service.Setup(x => x.GetWorkshopModUpdatedDate("123")).ReturnsAsync(updatedDate);

        var result = await _subject.GetWorkshopModUpdatedDate("123");

        result.UpdatedDate.Should().Be(updatedDate.ToString("o"));
    }

    [Fact]
    public async Task InstallWorkshopMod_ShouldCallService()
    {
        var request = new InstallWorkshopModRequest { SteamId = "123", RootMod = true };

        await _subject.InstallWorkshopMod(request);

        _service.Verify(x => x.InstallWorkshopMod("123", true), Times.Once);
    }

    [Fact]
    public async Task UpdateWorkshopMod_ShouldCallService()
    {
        await _subject.UpdateWorkshopMod("123");

        _service.Verify(x => x.UpdateWorkshopMod("123"), Times.Once);
    }

    [Fact]
    public async Task UninstallWorkshopMod_ShouldCallService()
    {
        await _subject.UninstallWorkshopMod("123");

        _service.Verify(x => x.UninstallWorkshopMod("123"), Times.Once);
    }

    [Fact]
    public async Task ResolveWorkshopModManualIntervention_ShouldCallService()
    {
        var request = new WorkshopModResolveInterventionRequest { SelectedPbos = ["a.pbo", "b.pbo"] };

        await _subject.ResolveWorkshopModManualIntervention("123", request);

        _service.Verify(x => x.ResolveWorkshopModManualIntervention("123", request.SelectedPbos), Times.Once);
    }

    [Fact]
    public async Task DeleteWorkshopMod_ShouldCallService()
    {
        await _subject.DeleteWorkshopMod("123");

        _service.Verify(x => x.DeleteWorkshopMod("123"), Times.Once);
    }
}
