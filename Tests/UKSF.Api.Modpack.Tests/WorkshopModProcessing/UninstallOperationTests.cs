using FluentAssertions;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;
using Xunit;

namespace UKSF.Api.Modpack.Tests.WorkshopModProcessing;

public class UninstallOperationTests
{
    private readonly Mock<IWorkshopModsContext> _mockContext;
    private readonly Mock<IWorkshopModsProcessingService> _mockProcessingService;
    private readonly UninstallOperation _uninstallOperation;

    public UninstallOperationTests()
    {
        _mockContext = new Mock<IWorkshopModsContext>();
        _mockProcessingService = new Mock<IWorkshopModsProcessingService>();
        var mockLogger = new Mock<IUksfLogger>();
        _uninstallOperation = new UninstallOperation(_mockContext.Object, _mockProcessingService.Object, mockLogger.Object);
    }

    [Fact]
    public async Task UninstallAsync_WithValidWorkshopMod_ShouldSucceed()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var pbos = new List<string> { "mod1.pbo", "mod2.pbo" };
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = WorkshopModId,
            Pbos = pbos,
            Status = WorkshopModStatus.Installed
        };

        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        // Act
        var result = await _uninstallOperation.UninstallAsync(WorkshopModId);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Uninstalling, "Uninstalling..."), Times.Once);
        _mockProcessingService.Verify(x => x.DeletePbosFromDependencies(pbos), Times.Once);
        workshopMod.Pbos.Should().BeEmpty();
        workshopMod.Status.Should().Be(WorkshopModStatus.UninstalledPendingRelease);
        workshopMod.ErrorMessage.Should().BeNull();
        _mockContext.Verify(x => x.Replace(workshopMod), Times.Once);
    }

    [Fact]
    public async Task UninstallAsync_WithAlreadyUninstalledMod_ShouldReturnSuccessIdempotently()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = WorkshopModId,
            Status = WorkshopModStatus.Uninstalled,
            Pbos = []
        };

        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);

        // Act
        var result = await _uninstallOperation.UninstallAsync(WorkshopModId);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        _mockProcessingService.Verify(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>()), Times.Never);
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainWorkshopMod>()), Times.Never);
    }

    [Fact]
    public async Task UninstallAsync_WithNoPbos_ShouldStillSucceed()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = WorkshopModId,
            Pbos = [],
            Status = WorkshopModStatus.Installed // Changed from Installing to Installed
        };

        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        // Act
        var result = await _uninstallOperation.UninstallAsync(WorkshopModId);

        // Assert
        result.Success.Should().BeTrue();
        _mockProcessingService.Verify(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>()), Times.Never);
        _mockContext.Verify(x => x.Replace(workshopMod), Times.Once);
    }

    [Fact]
    public async Task UninstallAsync_WhenDeleteFails_ShouldReturnFailure()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var pbos = new List<string> { "mod1.pbo" };
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = WorkshopModId,
            Pbos = pbos,
            Status = WorkshopModStatus.Installed
        };

        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _mockProcessingService.Setup(x => x.DeletePbosFromDependencies(pbos)).Throws(new IOException("Delete failed"));

        // Act
        var result = await _uninstallOperation.UninstallAsync(WorkshopModId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Delete failed");
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UninstallAsync_WhenCancelled_ShouldThrowOperationCancelledException()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = WorkshopModId,
            Pbos = ["mod1.pbo"],
            Status = WorkshopModStatus.Installed
        };
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _mockProcessingService.Setup(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>())).Throws(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _uninstallOperation.UninstallAsync(WorkshopModId, cancellationTokenSource.Token));
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Uninstall cancelled"), Times.Once);
    }

    [Fact]
    public async Task UninstallAsync_ForRootMod_CallsDeleteRootModFromRepos()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = WorkshopModId,
            Name = "Test Mod",
            RootMod = true,
            Status = WorkshopModStatus.Installed
        };

        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        // Act
        var result = await _uninstallOperation.UninstallAsync(WorkshopModId);

        // Assert
        result.Success.Should().BeTrue();
        _mockProcessingService.Verify(x => x.DeleteRootModFromRepos(workshopMod), Times.Once);
        _mockProcessingService.Verify(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>()), Times.Never);
        workshopMod.Status.Should().Be(WorkshopModStatus.UninstalledPendingRelease);
    }

    [Fact]
    public async Task UninstallAsync_ForRootMod_WhenDeleteFails_ShouldReturnFailure()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = WorkshopModId,
            Name = "Test Mod",
            RootMod = true,
            Status = WorkshopModStatus.Installed
        };

        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _mockProcessingService.Setup(x => x.DeleteRootModFromRepos(workshopMod)).Throws(new IOException("Delete failed"));

        // Act
        var result = await _uninstallOperation.UninstallAsync(WorkshopModId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Delete failed");
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UninstallAsync_WhenAlreadyUninstalled_ReturnsSuccessWithNoFilesChanged()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = WorkshopModId,
            Status = WorkshopModStatus.Uninstalled,
            Pbos = []
        };

        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);

        // Act
        var result = await _uninstallOperation.UninstallAsync(WorkshopModId);

        // Assert
        result.Success.Should().BeTrue();
        result.FilesChanged.Should().BeFalse();
    }

    [Fact]
    public async Task UninstallAsync_WhenNoPbosAndNotRootMod_ReturnsSuccessWithNoFilesChanged()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = WorkshopModId,
            Pbos = [],
            RootMod = false,
            Status = WorkshopModStatus.Installing
        };

        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        // Act
        var result = await _uninstallOperation.UninstallAsync(WorkshopModId);

        // Assert
        result.Success.Should().BeTrue();
        result.FilesChanged.Should().BeFalse();
        _mockProcessingService.Verify(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task UninstallAsync_WhenHasPbos_ReturnsSuccessWithFilesChanged()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var pbos = new List<string> { "mod1.pbo", "mod2.pbo" };
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = WorkshopModId,
            Pbos = pbos,
            RootMod = false,
            Status = WorkshopModStatus.Installed
        };

        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        // Act
        var result = await _uninstallOperation.UninstallAsync(WorkshopModId);

        // Assert
        result.Success.Should().BeTrue();
        result.FilesChanged.Should().BeTrue();
        _mockProcessingService.Verify(x => x.DeletePbosFromDependencies(pbos), Times.Once);
    }

    [Fact]
    public async Task UninstallAsync_WhenRootMod_ReturnsSuccessWithFilesChanged()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = WorkshopModId,
            Name = "Test Mod",
            RootMod = true,
            Status = WorkshopModStatus.Installed
        };

        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        // Act
        var result = await _uninstallOperation.UninstallAsync(WorkshopModId);

        // Assert
        result.Success.Should().BeTrue();
        result.FilesChanged.Should().BeTrue();
        _mockProcessingService.Verify(x => x.DeleteRootModFromRepos(workshopMod), Times.Once);
    }
}
