using FluentAssertions;
using Moq;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;
using Xunit;

namespace UKSF.Api.Modpack.Tests.WorkshopModProcessing;

public class UninstallOperationTests
{
    private readonly Mock<IWorkshopModsContext> _mockContext;
    private readonly Mock<IWorkshopModsProcessingService> _mockProcessingService;
    private readonly UninstallOperation _operation;

    public UninstallOperationTests()
    {
        _mockContext = new Mock<IWorkshopModsContext>();
        _mockProcessingService = new Mock<IWorkshopModsProcessingService>();
        _operation = new UninstallOperation(_mockContext.Object, _mockProcessingService.Object);
    }

    private DomainWorkshopMod SetupWorkshopMod(
        string workshopModId = "test-mod-123",
        bool rootMod = false,
        WorkshopModStatus status = WorkshopModStatus.Installed,
        List<string> pbos = null
    )
    {
        var workshopMod = new DomainWorkshopMod
        {
            Id = workshopModId,
            SteamId = workshopModId,
            Name = "Test Mod",
            RootMod = rootMod,
            Status = status,
            Pbos = pbos ?? []
        };
        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        return workshopMod;
    }

    [Fact]
    public async Task ExecuteAsync_WithValidWorkshopMod_ShouldSucceed()
    {
        var pbos = new List<string> { "mod1.pbo", "mod2.pbo" };
        var workshopMod = SetupWorkshopMod(pbos: pbos);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        var result = await _operation.ExecuteAsync("test-mod-123", []);

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Uninstalling, "Uninstalling..."), Times.Once);
        _mockProcessingService.Verify(x => x.DeletePbosFromDependencies(pbos), Times.Once);
        workshopMod.Pbos.Should().BeEmpty();
        workshopMod.ErrorMessage.Should().BeNull();
        _mockContext.Verify(x => x.Replace(workshopMod), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithAlreadyUninstalledMod_ShouldReturnSuccessWithNoFilesChanged()
    {
        SetupWorkshopMod(status: WorkshopModStatus.Uninstalled);

        var result = await _operation.ExecuteAsync("test-mod-123", []);

        result.Success.Should().BeTrue();
        result.FilesChanged.Should().BeFalse();
        _mockProcessingService.Verify(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>()), Times.Never);
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainWorkshopMod>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithUninstalledPendingReleaseMod_ShouldReturnSuccessWithNoFilesChanged()
    {
        SetupWorkshopMod(status: WorkshopModStatus.UninstalledPendingRelease);

        var result = await _operation.ExecuteAsync("test-mod-123", []);

        result.Success.Should().BeTrue();
        result.FilesChanged.Should().BeFalse();
        _mockProcessingService.Verify(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>()), Times.Never);
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainWorkshopMod>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoPbosAndNotRootMod_ShouldSucceedWithNoFilesChanged()
    {
        SetupWorkshopMod();
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        var result = await _operation.ExecuteAsync("test-mod-123", []);

        result.Success.Should().BeTrue();
        result.FilesChanged.Should().BeFalse();
        _mockProcessingService.Verify(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithPbos_ShouldReturnFilesChanged()
    {
        var pbos = new List<string> { "mod1.pbo", "mod2.pbo" };
        SetupWorkshopMod(pbos: pbos);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        var result = await _operation.ExecuteAsync("test-mod-123", []);

        result.Success.Should().BeTrue();
        result.FilesChanged.Should().BeTrue();
        _mockProcessingService.Verify(x => x.DeletePbosFromDependencies(pbos), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ForRootMod_ShouldDeleteRootModAndReturnFilesChanged()
    {
        var workshopMod = SetupWorkshopMod(rootMod: true);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        var result = await _operation.ExecuteAsync("test-mod-123", []);

        result.Success.Should().BeTrue();
        result.FilesChanged.Should().BeTrue();
        _mockProcessingService.Verify(x => x.DeleteRootModFromRepos(workshopMod), Times.Once);
        _mockProcessingService.Verify(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithInstalledStatus_ShouldSetUninstalledPendingRelease()
    {
        var workshopMod = SetupWorkshopMod(status: WorkshopModStatus.Installed);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        await _operation.ExecuteAsync("test-mod-123", []);

        workshopMod.Status.Should().Be(WorkshopModStatus.UninstalledPendingRelease);
        workshopMod.StatusMessage.Should().Be("Uninstalled pending next modpack release");
    }

    [Fact]
    public async Task ExecuteAsync_WithInstalledPendingReleaseStatus_ShouldSetUninstalledPendingRelease()
    {
        var workshopMod = SetupWorkshopMod(status: WorkshopModStatus.InstalledPendingRelease);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        await _operation.ExecuteAsync("test-mod-123", []);

        workshopMod.Status.Should().Be(WorkshopModStatus.UninstalledPendingRelease);
        workshopMod.StatusMessage.Should().Be("Uninstalled pending next modpack release");
    }

    [Fact]
    public async Task ExecuteAsync_WithUpdatedPendingReleaseStatus_ShouldSetUninstalledPendingRelease()
    {
        var workshopMod = SetupWorkshopMod(status: WorkshopModStatus.UpdatedPendingRelease);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        await _operation.ExecuteAsync("test-mod-123", []);

        workshopMod.Status.Should().Be(WorkshopModStatus.UninstalledPendingRelease);
        workshopMod.StatusMessage.Should().Be("Uninstalled pending next modpack release");
    }

    [Fact]
    public async Task ExecuteAsync_WithInstallingStatus_ShouldSetUninstalled()
    {
        var workshopMod = SetupWorkshopMod(status: WorkshopModStatus.Installing);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        await _operation.ExecuteAsync("test-mod-123", []);

        workshopMod.Status.Should().Be(WorkshopModStatus.Uninstalled);
        workshopMod.StatusMessage.Should().Be("Uninstalled");
    }

    [Fact]
    public async Task ExecuteAsync_WhenModNotFound_ShouldReturnFailure()
    {
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>())).Returns((DomainWorkshopMod)null);

        var result = await _operation.ExecuteAsync("missing-mod", []);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeleteFails_ShouldReturnFailureWithExceptionMessage()
    {
        var pbos = new List<string> { "mod1.pbo" };
        SetupWorkshopMod(pbos: pbos);
        _mockProcessingService.Setup(x => x.DeletePbosFromDependencies(pbos)).Throws(new IOException("Delete failed"));

        var result = await _operation.ExecuteAsync("test-mod-123", []);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Delete failed");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeleteFails_ShouldNotUpdateStatusToError()
    {
        var pbos = new List<string> { "mod1.pbo" };
        SetupWorkshopMod(pbos: pbos);
        _mockProcessingService.Setup(x => x.DeletePbosFromDependencies(pbos)).Throws(new IOException("Delete failed"));

        await _operation.ExecuteAsync("test-mod-123", []);

        _mockProcessingService.Verify(x => x.UpdateModStatus(It.IsAny<DomainWorkshopMod>(), WorkshopModStatus.Error, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ForRootMod_WhenDeleteFails_ShouldReturnFailure()
    {
        var workshopMod = SetupWorkshopMod(rootMod: true);
        _mockProcessingService.Setup(x => x.DeleteRootModFromRepos(workshopMod)).Throws(new IOException("Delete failed"));

        var result = await _operation.ExecuteAsync("test-mod-123", []);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Delete failed");
    }

    [Fact]
    public async Task ExecuteAsync_ForRootMod_WhenDeleteFails_ShouldNotUpdateStatusToError()
    {
        var workshopMod = SetupWorkshopMod(rootMod: true);
        _mockProcessingService.Setup(x => x.DeleteRootModFromRepos(workshopMod)).Throws(new IOException("Delete failed"));

        await _operation.ExecuteAsync("test-mod-123", []);

        _mockProcessingService.Verify(x => x.UpdateModStatus(It.IsAny<DomainWorkshopMod>(), WorkshopModStatus.Error, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ShouldThrowOperationCancelledException()
    {
        SetupWorkshopMod(pbos: ["mod1.pbo"]);
        _mockProcessingService.Setup(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>())).Throws(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => _operation.ExecuteAsync("test-mod-123", []));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ShouldUpdateStatusToError()
    {
        var workshopMod = SetupWorkshopMod(pbos: ["mod1.pbo"]);
        _mockProcessingService.Setup(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>())).Throws(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => _operation.ExecuteAsync("test-mod-123", []));

        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Uninstall cancelled"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldClearPbosAndSetLastUpdatedOnSuccess()
    {
        var pbos = new List<string> { "mod1.pbo", "mod2.pbo" };
        var workshopMod = SetupWorkshopMod(pbos: pbos);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        await _operation.ExecuteAsync("test-mod-123", []);

        workshopMod.Pbos.Should().BeEmpty();
        workshopMod.ErrorMessage.Should().BeNull();
        workshopMod.LastUpdatedLocally.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
