using FluentAssertions;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using Xunit;

namespace UKSF.Api.Modpack.Tests.Services;

public class WorkshopModsProcessingServiceTests
{
    private readonly Mock<IWorkshopModsContext> _context = new();
    private readonly Mock<IVariablesService> _variablesService = new();
    private readonly Mock<IModpackService> _modpackService = new();
    private readonly Mock<IFileSystemService> _fileSystemService = new();
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly WorkshopModsProcessingService _subject;

    public WorkshopModsProcessingServiceTests()
    {
        _subject = new WorkshopModsProcessingService(
            _context.Object,
            _variablesService.Object,
            new Mock<ISteamCmdService>().Object,
            _modpackService.Object,
            _fileSystemService.Object,
            _logger.Object
        );
    }

    [Fact]
    public void GetWorkshopModPath_ShouldCombineSteamPath()
    {
        _variablesService.Setup(x => x.GetVariable("SERVER_PATH_STEAM")).Returns(new DomainVariableItem { Key = "SERVER_PATH_STEAM", Item = "C:\\steam" });

        var result = _subject.GetWorkshopModPath("123");

        result.Should().Be(Path.Combine("C:\\steam", "steamapps", "workshop", "content", "107410", "123"));
    }

    private const string ModPath = "C:\\mod";
    private static readonly string AddonsPath = Path.Combine(ModPath, "addons");

    private void SetupAddons(params string[] pboPaths)
    {
        _fileSystemService.Setup(x => x.DirectoryExists(AddonsPath)).Returns(true);
        _fileSystemService.Setup(x => x.EnumerateFiles(AddonsPath, "*.pbo", SearchOption.AllDirectories)).Returns(pboPaths);
    }

    [Fact]
    public void GetPboFiles_ShouldReturnPboFileNamesFromAddons()
    {
        SetupAddons(Path.Combine(AddonsPath, "a.pbo"), Path.Combine(AddonsPath, "optional", "b.pbo"));

        var result = _subject.GetPboFiles(ModPath);

        result.Should().BeEquivalentTo("a.pbo", "b.pbo");
    }

    [Fact]
    public void GetPboFiles_WhenNoAddonsDirectory_ShouldReturnEmpty()
    {
        _fileSystemService.Setup(x => x.DirectoryExists(AddonsPath)).Returns(false);

        var result = _subject.GetPboFiles(ModPath);

        result.Should().BeEmpty();
        _fileSystemService.Verify(x => x.EnumerateFiles(It.IsAny<string>(), "*.pbo", It.IsAny<SearchOption>()), Times.Never);
    }

    [Fact]
    public void GetPboFiles_WhenDuplicateNames_ShouldThrow()
    {
        SetupAddons(Path.Combine(AddonsPath, "a.pbo"), Path.Combine(AddonsPath, "optional", "a.pbo"));

        var action = () => _subject.GetPboFiles(ModPath);

        action.Should().Throw<InvalidOperationException>().WithMessage("*Duplicate PBO names*");
    }

    [Fact]
    public void GetExtensions_ShouldReturnRootDllNames()
    {
        _fileSystemService.Setup(x => x.EnumerateFiles(ModPath, "*.dll", SearchOption.TopDirectoryOnly))
                          .Returns([Path.Combine(ModPath, "ctab_connect.dll"), Path.Combine(ModPath, "ctab_connect_x64.dll")]);

        var result = _subject.GetExtensions(ModPath);

        result.Should().BeEquivalentTo("ctab_connect.dll", "ctab_connect_x64.dll");
    }

    [Fact]
    public void GetExtensions_WhenNoDlls_ShouldReturnEmpty()
    {
        _fileSystemService.Setup(x => x.EnumerateFiles(ModPath, "*.dll", SearchOption.TopDirectoryOnly)).Returns([]);

        var result = _subject.GetExtensions(ModPath);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task QueueDevBuild_ShouldCancelRunningBuildsAndStartNew()
    {
        var runningBuild = new DomainModpackBuild { Running = true };
        _modpackService.Setup(x => x.GetDevBuilds()).Returns([runningBuild]);
        _modpackService.Setup(x => x.CancelBuild(runningBuild)).Returns(Task.CompletedTask);
        _modpackService.Setup(x => x.NewBuild(It.IsAny<NewBuild>())).Returns(Task.CompletedTask);

        await _subject.QueueDevBuild("cTAB Connect", WorkshopModStatus.InstalledPendingRelease);

        _modpackService.Verify(x => x.CancelBuild(runningBuild), Times.Once);
        _modpackService.Verify(x => x.NewBuild(It.Is<NewBuild>(b => b.Reference == "main")), Times.Once);
    }

    [Fact]
    public async Task QueueDevBuild_WhenNoRunningBuilds_ShouldOnlyStartNew()
    {
        _modpackService.Setup(x => x.GetDevBuilds()).Returns([]);
        _modpackService.Setup(x => x.NewBuild(It.IsAny<NewBuild>())).Returns(Task.CompletedTask);

        await _subject.QueueDevBuild("cTAB Connect", WorkshopModStatus.InstalledPendingRelease);

        _modpackService.Verify(x => x.CancelBuild(It.IsAny<DomainModpackBuild>()), Times.Never);
        _modpackService.Verify(x => x.NewBuild(It.Is<NewBuild>(b => b.Reference == "main")), Times.Once);
    }

    [Theory]
    [InlineData(WorkshopModStatus.InstalledPendingRelease, "#### Added\n- cTAB Connect")]
    [InlineData(WorkshopModStatus.UpdatedPendingRelease, "#### Updated\n- cTAB Connect")]
    [InlineData(WorkshopModStatus.UninstalledPendingRelease, "#### Removed\n- cTAB Connect")]
    [InlineData(WorkshopModStatus.Uninstalled, "#### Removed\n- cTAB Connect")]
    public async Task QueueDevBuild_ShouldDescribeTheWorkshopModAsTheBuildChanges(WorkshopModStatus status, string expectedChanges)
    {
        _modpackService.Setup(x => x.GetDevBuilds()).Returns([]);
        _modpackService.Setup(x => x.NewBuild(It.IsAny<NewBuild>())).Returns(Task.CompletedTask);

        await _subject.QueueDevBuild("cTAB Connect", status);

        _modpackService.Verify(x => x.NewBuild(It.Is<NewBuild>(b => b.Changes == expectedChanges)), Times.Once);
    }

    [Fact]
    public async Task QueueDevBuild_WhenSkipVariableSet_ShouldNotQueueBuild()
    {
        _variablesService.Setup(x => x.GetVariable("WORKSHOP_SKIP_DEV_BUILD")).Returns(new DomainVariableItem { Key = "WORKSHOP_SKIP_DEV_BUILD", Item = true });

        await _subject.QueueDevBuild("cTAB Connect", WorkshopModStatus.InstalledPendingRelease);

        _modpackService.Verify(x => x.NewBuild(It.IsAny<NewBuild>()), Times.Never);
    }

    [Fact]
    public async Task QueueDevBuild_WhenExceptionThrown_ShouldLogErrorAndNotRethrow()
    {
        _modpackService.Setup(x => x.GetDevBuilds()).Throws(new Exception("test error"));

        await _subject.QueueDevBuild("cTAB Connect", WorkshopModStatus.InstalledPendingRelease);

        _logger.Verify(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
    }

    [Fact]
    public async Task UpdateModStatus_WhenError_ShouldSetErrorMessage()
    {
        var workshopMod = new DomainWorkshopMod { Id = "mod-id" };
        _context.Setup(x => x.Replace(workshopMod)).Returns(Task.CompletedTask);

        await _subject.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "failed");

        workshopMod.ErrorMessage.Should().Be("failed");
        _context.Verify(x => x.Replace(workshopMod), Times.Once);
    }

    [Fact]
    public async Task UpdateModStatus_WhenNonError_ShouldSetStatusMessage()
    {
        var workshopMod = new DomainWorkshopMod { Id = "mod-id" };
        _context.Setup(x => x.Replace(workshopMod)).Returns(Task.CompletedTask);

        await _subject.UpdateModStatus(workshopMod, WorkshopModStatus.Installing, "working");

        workshopMod.StatusMessage.Should().Be("working");
        _context.Verify(x => x.Replace(workshopMod), Times.Once);
    }

    [Fact]
    public void CleanupWorkshopModFiles_WhenDirectoryExists_ShouldDelete()
    {
        _fileSystemService.Setup(x => x.DirectoryExists("C:\\workshop\\mod")).Returns(true);

        _subject.CleanupWorkshopModFiles("C:\\workshop\\mod");

        _fileSystemService.Verify(x => x.DeleteDirectory("C:\\workshop\\mod", true), Times.Once);
    }

    [Fact]
    public void CleanupWorkshopModFiles_WhenDirectoryDoesNotExist_ShouldNotDelete()
    {
        _fileSystemService.Setup(x => x.DirectoryExists("C:\\workshop\\mod")).Returns(false);

        _subject.CleanupWorkshopModFiles("C:\\workshop\\mod");

        _fileSystemService.Verify(x => x.DeleteDirectory(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task SetAvailable_ShouldWriteAvailableLists_AndLeaveInstalledPbosUntouched()
    {
        var existingInstalled = new List<string> { "a.pbo", "b.pbo" };
        var candidate = new List<string> { "b.pbo", "c.pbo" };
        var workshopMod = new DomainWorkshopMod { SteamId = "123", Pbos = [..existingInstalled] };
        _context.Setup(x => x.Replace(workshopMod)).Returns(Task.CompletedTask);

        await _subject.SetAvailable(workshopMod, candidate, ["extension.dll"]);

        workshopMod.AvailablePbos.Should().BeEquivalentTo(candidate);
        workshopMod.AvailableExtensions.Should().BeEquivalentTo("extension.dll");
        workshopMod.Pbos.Should().BeEquivalentTo(existingInstalled);
        _context.Verify(x => x.Replace(workshopMod), Times.Once);
    }
}
