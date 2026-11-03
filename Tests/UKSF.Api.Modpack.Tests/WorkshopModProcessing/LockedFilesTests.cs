using FluentAssertions;
using Moq;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;
using Xunit;

namespace UKSF.Api.Modpack.Tests.WorkshopModProcessing;

public class LockedFilesTests
{
    private readonly Mock<IWorkshopModsContext> _mockContext = new();
    private readonly Mock<IWorkshopModsProcessingService> _mockProcessingService = new();
    private readonly Mock<IWorkshopModDependencyFilesService> _mockDependencyFilesService = new();
    private readonly Mock<IWorkshopModRootFilesService> _mockRootFilesService = new();
    private readonly UpdateOperation _update;
    private readonly InstallOperation _install;

    public LockedFilesTests()
    {
        _mockProcessingService.Setup(x => x.GetWorkshopModPath("test-mod-123")).Returns("/path/to/mod");
        _mockProcessingService.Setup(x => x.GetExtensions(It.IsAny<string>())).Returns([]);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);
        _update = new UpdateOperation(_mockContext.Object, _mockProcessingService.Object, _mockDependencyFilesService.Object, _mockRootFilesService.Object);
        _install = new InstallOperation(_mockContext.Object, _mockProcessingService.Object, _mockDependencyFilesService.Object, _mockRootFilesService.Object);
    }

    private DomainWorkshopMod SetupWorkshopMod(List<string> pbos, List<string> lockedPbos, List<string> extensions = null, List<string> lockedExtensions = null)
    {
        var workshopMod = new DomainWorkshopMod
        {
            Id = "test-mod-123",
            SteamId = "test-mod-123",
            Name = "ATAK",
            Status = WorkshopModStatus.Updating,
            Pbos = pbos,
            LockedPbos = lockedPbos,
            Extensions = extensions ?? [],
            LockedExtensions = lockedExtensions ?? []
        };
        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        return workshopMod;
    }

    private void SetupFilesOnDisk(List<string> pbos, List<string> extensions = null)
    {
        _mockProcessingService.Setup(x => x.GetPboFiles("/path/to/mod")).Returns(pbos);
        _mockProcessingService.Setup(x => x.GetExtensions("/path/to/mod")).Returns(extensions ?? []);
    }

    [Fact]
    public async Task Check_WhenEveryAvailableFileIsInstalledOrLocked_ShouldContinueWithOnlyTheInstalledFiles()
    {
        SetupWorkshopMod(pbos: ["ATAK_Phone.pbo"], lockedPbos: ["ATAK_Alerts.pbo", "ACRE_MPU5.pbo"], extensions: ["atak.dll"], lockedExtensions: ["other.dll"]);
        SetupFilesOnDisk(["ACRE_MPU5.pbo", "ATAK_Alerts.pbo", "ATAK_Phone.pbo"], ["atak.dll", "other.dll"]);

        var result = await _update.CheckAsync("test-mod-123");

        result.InterventionRequired.Should().BeFalse();
        result.AvailablePbos.Should().BeEquivalentTo("ATAK_Phone.pbo");
        result.AvailableExtensions.Should().BeEquivalentTo("atak.dll");
    }

    [Fact]
    public async Task Check_WhenAPboHasNeverBeenSeen_ShouldRequireIntervention()
    {
        SetupWorkshopMod(pbos: ["ATAK_Phone.pbo"], lockedPbos: ["ATAK_Alerts.pbo"]);
        SetupFilesOnDisk(["ATAK_Alerts.pbo", "ATAK_Phone.pbo", "ATAK_Brand_New.pbo"]);

        var result = await _update.CheckAsync("test-mod-123");

        result.InterventionRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Check_WhenAnExtensionHasNeverBeenSeen_ShouldRequireIntervention()
    {
        SetupWorkshopMod(pbos: ["ATAK_Phone.pbo"], lockedPbos: [], extensions: ["atak.dll"], lockedExtensions: ["other.dll"]);
        SetupFilesOnDisk(["ATAK_Phone.pbo"], ["atak.dll", "other.dll", "new.dll"]);

        var result = await _update.CheckAsync("test-mod-123");

        result.InterventionRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Check_WhenAnInstalledPboIsGone_ShouldRequireIntervention()
    {
        SetupWorkshopMod(pbos: ["ATAK_Phone.pbo", "ATAK_Removed.pbo"], lockedPbos: ["ATAK_Alerts.pbo"]);
        SetupFilesOnDisk(["ATAK_Alerts.pbo", "ATAK_Phone.pbo"]);

        var result = await _update.CheckAsync("test-mod-123");

        result.InterventionRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Check_WhenALockedPboIsGone_ShouldNotRequireIntervention()
    {
        SetupWorkshopMod(pbos: ["ATAK_Phone.pbo"], lockedPbos: ["ATAK_Alerts.pbo"]);
        SetupFilesOnDisk(["ATAK_Phone.pbo"]);

        var result = await _update.CheckAsync("test-mod-123");

        result.InterventionRequired.Should().BeFalse();
        result.AvailablePbos.Should().BeEquivalentTo("ATAK_Phone.pbo");
    }

    [Fact]
    public async Task Execute_ShouldLockEveryAvailableFileThatWasNotSelected()
    {
        var workshopMod = SetupWorkshopMod(pbos: [], lockedPbos: []);
        workshopMod.AvailablePbos = ["ACRE_MPU5.pbo", "ATAK_Alerts.pbo", "ATAK_Phone.pbo"];
        workshopMod.AvailableExtensions = ["atak.dll", "other.dll"];

        await _install.ExecuteAsync("test-mod-123", ["ATAK_Phone.pbo"], ["atak.dll"]);

        workshopMod.Pbos.Should().BeEquivalentTo("ATAK_Phone.pbo");
        workshopMod.LockedPbos.Should().BeEquivalentTo("ACRE_MPU5.pbo", "ATAK_Alerts.pbo");
        workshopMod.LockedExtensions.Should().BeEquivalentTo("other.dll");
    }

    [Fact]
    public async Task Execute_WhenALockedFileIsSelected_ShouldUnlockIt()
    {
        var workshopMod = SetupWorkshopMod(pbos: ["ATAK_Phone.pbo"], lockedPbos: ["ATAK_Alerts.pbo", "ACRE_MPU5.pbo"]);
        workshopMod.AvailablePbos = ["ACRE_MPU5.pbo", "ATAK_Alerts.pbo", "ATAK_Phone.pbo"];

        await _update.ExecuteAsync("test-mod-123", ["ATAK_Phone.pbo", "ATAK_Alerts.pbo"], []);

        workshopMod.LockedPbos.Should().BeEquivalentTo("ACRE_MPU5.pbo");
    }
}
