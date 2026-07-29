using FluentAssertions;
using Moq;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;
using Xunit;

namespace UKSF.Api.Modpack.Tests.WorkshopModProcessing;

public class InstallOperationTests
{
    private readonly Mock<IWorkshopModsContext> _mockContext = new();
    private readonly Mock<IWorkshopModsProcessingService> _mockProcessingService = new();
    private readonly Mock<IWorkshopModDependencyFilesService> _mockDependencyFilesService = new();
    private readonly Mock<IWorkshopModRootFilesService> _mockRootFilesService = new();
    private readonly InstallOperation _operation;

    public InstallOperationTests()
    {
        _mockProcessingService.Setup(x => x.GetWorkshopModPath("test-mod-123")).Returns("/path/to/mod");
        _mockProcessingService.Setup(x => x.GetExtensionFiles(It.IsAny<string>())).Returns([]);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);
        _operation = new InstallOperation(_mockContext.Object, _mockProcessingService.Object, _mockDependencyFilesService.Object, _mockRootFilesService.Object);
    }

    private DomainWorkshopMod SetupWorkshopMod(bool rootMod = false, List<string> pbos = null)
    {
        var workshopMod = new DomainWorkshopMod
        {
            Id = "test-mod-123",
            SteamId = "test-mod-123",
            Name = "Test Mod",
            RootMod = rootMod,
            Status = WorkshopModStatus.Installing,
            Pbos = pbos ?? []
        };
        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        return workshopMod;
    }

    [Fact]
    public async Task DownloadAsync_WithValidWorkshopMod_ShouldSucceed()
    {
        var workshopMod = SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.DownloadWithRetries("test-mod-123", It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _operation.DownloadAsync("test-mod-123");

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Installing, "Downloading..."), Times.Once);
        _mockProcessingService.Verify(x => x.DownloadWithRetries("test-mod-123", It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadAsync_WhenDownloadFails_ShouldReturnFailureWithoutErrorStatus()
    {
        SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.DownloadWithRetries("test-mod-123", It.IsAny<int>(), It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new Exception("Download failed"));

        var result = await _operation.DownloadAsync("test-mod-123");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Download failed");
        _mockProcessingService.Verify(x => x.UpdateModStatus(It.IsAny<DomainWorkshopMod>(), WorkshopModStatus.Error, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DownloadAsync_WhenCancelled_ShouldThrowOperationCancelledException()
    {
        var workshopMod = SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.DownloadWithRetries("test-mod-123", It.IsAny<int>(), It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => _operation.DownloadAsync("test-mod-123"));
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Install cancelled"), Times.Once);
    }

    [Fact]
    public async Task DownloadAsync_WhenModNotFound_ShouldReturnFailure()
    {
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>())).Returns((DomainWorkshopMod)null);

        var result = await _operation.DownloadAsync("missing-mod");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task CheckAsync_WithPbosChanged_ShouldRequireIntervention()
    {
        var workshopMod = SetupWorkshopMod();
        var pbos = new List<string> { "mod1.pbo", "mod2.pbo" };
        _mockProcessingService.Setup(x => x.GetPboFiles("/path/to/mod")).Returns(pbos);

        var result = await _operation.CheckAsync("test-mod-123");

        result.Success.Should().BeTrue();
        result.InterventionRequired.Should().BeTrue();
        result.AvailablePbos.Should().BeEquivalentTo(pbos);
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Installing, "Checking..."), Times.Once);
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.InterventionRequired, "Select files to install"), Times.Once);
        _mockProcessingService.Verify(x => x.SetAvailablePbos(workshopMod, pbos), Times.Once);
    }

    [Fact]
    public async Task CheckAsync_WithPbosUnchanged_ShouldNotRequireIntervention()
    {
        var pbos = new List<string> { "mod1.pbo", "mod2.pbo" };
        SetupWorkshopMod(pbos: pbos);
        _mockProcessingService.Setup(x => x.GetPboFiles("/path/to/mod")).Returns(pbos);

        var result = await _operation.CheckAsync("test-mod-123");

        result.Success.Should().BeTrue();
        result.InterventionRequired.Should().BeFalse();
        result.AvailablePbos.Should().BeEquivalentTo(pbos);
        _mockProcessingService.Verify(
            x => x.UpdateModStatus(It.IsAny<DomainWorkshopMod>(), WorkshopModStatus.InterventionRequired, It.IsAny<string>()),
            Times.Never
        );
        _mockProcessingService.Verify(x => x.SetAvailablePbos(It.IsAny<DomainWorkshopMod>(), pbos), Times.Once);
    }

    [Fact]
    public async Task CheckAsync_WithExtensionFilesAndNoPbos_ShouldSucceedWithoutIntervention()
    {
        SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.GetPboFiles("/path/to/mod")).Returns([]);
        _mockProcessingService.Setup(x => x.GetExtensionFiles("/path/to/mod")).Returns(["ctab_connect.dll"]);

        var result = await _operation.CheckAsync("test-mod-123");

        result.Success.Should().BeTrue();
        result.InterventionRequired.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_WithNoPbosAndNoExtensionFiles_ShouldReturnFailure()
    {
        SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.GetPboFiles("/path/to/mod")).Returns([]);

        var result = await _operation.CheckAsync("test-mod-123");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No PBO or extension files found");
    }

    [Fact]
    public async Task CheckAsync_ForRootMod_ShouldSkipFileScan()
    {
        SetupWorkshopMod(rootMod: true);

        var result = await _operation.CheckAsync("test-mod-123");

        result.Success.Should().BeTrue();
        result.InterventionRequired.Should().BeFalse();
        result.AvailablePbos.Should().BeNull();
        _mockProcessingService.Verify(x => x.GetPboFiles(It.IsAny<string>()), Times.Never);
        _mockProcessingService.Verify(x => x.SetAvailablePbos(It.IsAny<DomainWorkshopMod>(), It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task CheckAsync_WhenGetPboFilesFails_ShouldReturnFailureWithoutErrorStatus()
    {
        SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.GetPboFiles("/path/to/mod")).Throws(new InvalidOperationException("Duplicate PBOs found"));

        var result = await _operation.CheckAsync("test-mod-123");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Duplicate PBOs found");
        _mockProcessingService.Verify(x => x.UpdateModStatus(It.IsAny<DomainWorkshopMod>(), WorkshopModStatus.Error, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CheckAsync_WhenModNotFound_ShouldReturnFailure()
    {
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>())).Returns((DomainWorkshopMod)null);

        var result = await _operation.CheckAsync("missing-mod");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task ExecuteAsync_WithPboMod_ShouldCopyPbos()
    {
        var workshopMod = SetupWorkshopMod();
        var selectedPbos = new List<string> { "mod1.pbo", "mod2.pbo" };

        var result = await _operation.ExecuteAsync("test-mod-123", selectedPbos);

        result.Success.Should().BeTrue();
        workshopMod.Pbos.Should().BeEquivalentTo(selectedPbos);
        workshopMod.Status.Should().Be(WorkshopModStatus.InstalledPendingRelease);
        workshopMod.LastUpdatedLocally.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        workshopMod.ErrorMessage.Should().BeNull();
        _mockDependencyFilesService.Verify(x => x.CopyPbosToDependencies(workshopMod, selectedPbos, It.IsAny<CancellationToken>()), Times.Once);
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Installing, "Installing..."), Times.Once);
        _mockContext.Verify(x => x.Replace(workshopMod), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithExtensionFiles_ShouldCopyAndTrackThem()
    {
        var workshopMod = SetupWorkshopMod();
        var extensionFiles = new List<string> { "ctab_connect.dll", "ctab_connect_x64.dll" };
        _mockProcessingService.Setup(x => x.GetExtensionFiles("/path/to/mod")).Returns(extensionFiles);

        var result = await _operation.ExecuteAsync("test-mod-123", ["mod1.pbo"]);

        result.Success.Should().BeTrue();
        workshopMod.ExtensionFiles.Should().BeEquivalentTo(extensionFiles);
        _mockDependencyFilesService.Verify(x => x.CopyExtensionFilesToDependencies(workshopMod, extensionFiles, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithRootMod_ShouldSyncRootMod()
    {
        var workshopMod = SetupWorkshopMod(rootMod: true);
        _mockRootFilesService.Setup(x => x.SyncRootModToRepos(workshopMod)).Returns(true);

        var result = await _operation.ExecuteAsync("test-mod-123", []);

        result.Success.Should().BeTrue();
        result.FilesChanged.Should().BeTrue();
        _mockRootFilesService.Verify(x => x.SyncRootModToRepos(workshopMod), Times.Once);
        _mockDependencyFilesService.Verify(
            x => x.CopyPbosToDependencies(It.IsAny<DomainWorkshopMod>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        workshopMod.Status.Should().Be(WorkshopModStatus.InstalledPendingRelease);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCopyFails_ShouldReturnFailureWithoutErrorStatus()
    {
        var workshopMod = SetupWorkshopMod();
        var selectedPbos = new List<string> { "mod1.pbo" };
        _mockDependencyFilesService.Setup(x => x.CopyPbosToDependencies(workshopMod, selectedPbos, It.IsAny<CancellationToken>()))
                                   .Throws(new IOException("Copy failed"));

        var result = await _operation.ExecuteAsync("test-mod-123", selectedPbos);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Copy failed");
        _mockProcessingService.Verify(x => x.UpdateModStatus(It.IsAny<DomainWorkshopMod>(), WorkshopModStatus.Error, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ShouldThrow()
    {
        var workshopMod = SetupWorkshopMod();
        var selectedPbos = new List<string> { "mod1.pbo" };
        _mockDependencyFilesService.Setup(x => x.CopyPbosToDependencies(workshopMod, selectedPbos, It.IsAny<CancellationToken>()))
                                   .Throws(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => _operation.ExecuteAsync("test-mod-123", selectedPbos));
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Install cancelled"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_RootMod_WhenSyncFails_ShouldReturnFailure()
    {
        var workshopMod = SetupWorkshopMod(rootMod: true);
        _mockRootFilesService.Setup(x => x.SyncRootModToRepos(workshopMod)).Throws(new IOException("Copy failed"));

        var result = await _operation.ExecuteAsync("test-mod-123", []);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Copy failed");
    }

    [Fact]
    public async Task ExecuteAsync_WhenModNotFound_ShouldReturnFailure()
    {
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>())).Returns((DomainWorkshopMod)null);

        var result = await _operation.ExecuteAsync("missing-mod", []);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }
}
