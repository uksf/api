using FluentAssertions;
using Moq;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;
using Xunit;

namespace UKSF.Api.Modpack.Tests.WorkshopModProcessing;

public class UpdateOperationTests
{
    private readonly Mock<IWorkshopModsContext> _mockContext = new();
    private readonly Mock<IWorkshopModsProcessingService> _mockProcessingService = new();
    private readonly Mock<IWorkshopModDependencyFilesService> _mockDependencyFilesService = new();
    private readonly Mock<IWorkshopModRootFilesService> _mockRootFilesService = new();
    private readonly UpdateOperation _operation;

    public UpdateOperationTests()
    {
        _mockProcessingService.Setup(x => x.GetWorkshopModPath("test-mod-123")).Returns("/path/to/mod");
        _mockProcessingService.Setup(x => x.GetExtensionFiles(It.IsAny<string>())).Returns([]);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);
        _operation = new UpdateOperation(_mockContext.Object, _mockProcessingService.Object, _mockDependencyFilesService.Object, _mockRootFilesService.Object);
    }

    private DomainWorkshopMod SetupWorkshopMod(bool rootMod = false, List<string> pbos = null, List<string> extensionFiles = null)
    {
        var workshopMod = new DomainWorkshopMod
        {
            Id = "test-mod-123",
            SteamId = "test-mod-123",
            Name = "Test Mod",
            RootMod = rootMod,
            Status = WorkshopModStatus.Updating,
            Pbos = pbos ?? [],
            ExtensionFiles = extensionFiles ?? []
        };
        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        return workshopMod;
    }

    [Fact]
    public async Task DownloadAsync_WithValidWorkshopMod_ShouldSetUpdatingStatus()
    {
        var workshopMod = SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.DownloadWithRetries("test-mod-123", It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _operation.DownloadAsync("test-mod-123");

        result.Success.Should().BeTrue();
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Updating, "Downloading..."), Times.Once);
    }

    [Fact]
    public async Task DownloadAsync_WhenCancelled_ShouldReportUpdateCancelled()
    {
        var workshopMod = SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.DownloadWithRetries("test-mod-123", It.IsAny<int>(), It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => _operation.DownloadAsync("test-mod-123"));
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Update cancelled"), Times.Once);
    }

    [Fact]
    public async Task CheckAsync_WithPbosChanged_ShouldRequireInterventionAndPreserveInstalledPbos()
    {
        var installed = new List<string> { "old1.pbo", "old2.pbo" };
        var workshopMod = SetupWorkshopMod(pbos: installed);
        var candidate = new List<string> { "old1.pbo", "new1.pbo" };
        _mockProcessingService.Setup(x => x.GetPboFiles("/path/to/mod")).Returns(candidate);

        var result = await _operation.CheckAsync("test-mod-123");

        result.Success.Should().BeTrue();
        result.InterventionRequired.Should().BeTrue();
        result.AvailablePbos.Should().BeEquivalentTo(candidate);
        workshopMod.Pbos.Should().BeEquivalentTo(installed);
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Updating, "Checking..."), Times.Once);
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.InterventionRequired, "Select files to install"), Times.Once);
        _mockProcessingService.Verify(x => x.SetAvailablePbos(workshopMod, candidate), Times.Once);
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
        _mockProcessingService.Verify(
            x => x.UpdateModStatus(It.IsAny<DomainWorkshopMod>(), WorkshopModStatus.InterventionRequired, It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithRootMod_ShouldSyncToReposAndReportFilesChanged()
    {
        var workshopMod = SetupWorkshopMod(rootMod: true);
        _mockRootFilesService.Setup(x => x.SyncRootModToRepos(workshopMod)).Returns(true);

        var result = await _operation.ExecuteAsync("test-mod-123", []);

        result.Success.Should().BeTrue();
        result.FilesChanged.Should().BeTrue();
        _mockRootFilesService.Verify(x => x.SyncRootModToRepos(workshopMod), Times.Once);
        _mockRootFilesService.Verify(x => x.DeleteRootModFromRepos(workshopMod), Times.Never);
        _mockDependencyFilesService.Verify(
            x => x.CopyPbosToDependencies(It.IsAny<DomainWorkshopMod>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        workshopMod.Status.Should().Be(WorkshopModStatus.UpdatedPendingRelease);
    }

    [Fact]
    public async Task ExecuteAsync_WithRootMod_WhenNoFilesChanged_ShouldReportNoFilesChanged()
    {
        var workshopMod = SetupWorkshopMod(rootMod: true);
        _mockRootFilesService.Setup(x => x.SyncRootModToRepos(workshopMod)).Returns(false);

        var result = await _operation.ExecuteAsync("test-mod-123", []);

        result.Success.Should().BeTrue();
        result.FilesChanged.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithPboMod_ShouldCopyThenDeleteRemovedPbos()
    {
        var workshopMod = SetupWorkshopMod(pbos: ["old1.pbo", "old2.pbo", "kept.pbo"]);
        var selectedPbos = new List<string> { "kept.pbo", "new1.pbo" };

        var result = await _operation.ExecuteAsync("test-mod-123", selectedPbos);

        result.Success.Should().BeTrue();
        _mockDependencyFilesService.Verify(x => x.CopyPbosToDependencies(workshopMod, selectedPbos, It.IsAny<CancellationToken>()), Times.Once);
        _mockDependencyFilesService.Verify(
            x => x.DeletePbosFromDependencies(It.Is<List<string>>(pbos => pbos.Contains("old1.pbo") && pbos.Contains("old2.pbo") && pbos.Count == 2)),
            Times.Once
        );
        workshopMod.Pbos.Should().BeEquivalentTo(selectedPbos);
    }

    [Fact]
    public async Task ExecuteAsync_WithPboMod_WhenNoRemovedPbos_ShouldSkipDelete()
    {
        SetupWorkshopMod(pbos: ["mod1.pbo"]);

        await _operation.ExecuteAsync("test-mod-123", ["mod1.pbo", "mod2.pbo"]);

        _mockDependencyFilesService.Verify(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithExtensionFiles_ShouldCopyThenDeleteRemovedExtensionFiles()
    {
        var workshopMod = SetupWorkshopMod(pbos: ["mod1.pbo"], extensionFiles: ["old_extension.dll", "kept_extension.dll"]);
        var extensionFiles = new List<string> { "kept_extension.dll", "new_extension.dll" };
        _mockProcessingService.Setup(x => x.GetExtensionFiles("/path/to/mod")).Returns(extensionFiles);

        var result = await _operation.ExecuteAsync("test-mod-123", ["mod1.pbo"]);

        result.Success.Should().BeTrue();
        workshopMod.ExtensionFiles.Should().BeEquivalentTo(extensionFiles);
        _mockDependencyFilesService.Verify(x => x.CopyExtensionFilesToDependencies(workshopMod, extensionFiles, It.IsAny<CancellationToken>()), Times.Once);
        _mockDependencyFilesService.Verify(
            x => x.DeleteExtensionFilesFromDependencies(It.Is<List<string>>(files => files.Single() == "old_extension.dll")),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenExtensionFilesUnchanged_ShouldSkipDelete()
    {
        SetupWorkshopMod(pbos: ["mod1.pbo"], extensionFiles: ["extension.dll"]);
        _mockProcessingService.Setup(x => x.GetExtensionFiles("/path/to/mod")).Returns(["extension.dll"]);

        await _operation.ExecuteAsync("test-mod-123", ["mod1.pbo"]);

        _mockDependencyFilesService.Verify(x => x.DeleteExtensionFilesFromDependencies(It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_OnSuccess_ShouldCompleteAndClearAvailablePbos()
    {
        var workshopMod = SetupWorkshopMod(pbos: ["old.pbo"]);
        workshopMod.AvailablePbos = ["old.pbo", "new.pbo"];
        workshopMod.ErrorMessage = "Previous error";

        await _operation.ExecuteAsync("test-mod-123", ["new.pbo"]);

        workshopMod.AvailablePbos.Should().BeEmpty();
        workshopMod.Status.Should().Be(WorkshopModStatus.UpdatedPendingRelease);
        workshopMod.StatusMessage.Should().Be("Updated pending next modpack release");
        workshopMod.LastUpdatedLocally.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        workshopMod.ErrorMessage.Should().BeNull();
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Updating, "Updating..."), Times.Once);
        _mockContext.Verify(x => x.Replace(workshopMod), Times.Once);
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
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Update cancelled"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_RootMod_WhenSyncFails_ShouldReturnFailure()
    {
        var workshopMod = SetupWorkshopMod(rootMod: true);
        _mockRootFilesService.Setup(x => x.SyncRootModToRepos(workshopMod)).Throws(new IOException("Sync failed"));

        var result = await _operation.ExecuteAsync("test-mod-123", []);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Sync failed");
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
