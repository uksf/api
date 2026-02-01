using FluentAssertions;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;
using Xunit;

namespace UKSF.Api.Modpack.Tests.WorkshopModProcessing;

public class InstallOperationTests
{
    private readonly InstallOperation _installOperation;
    private readonly Mock<IWorkshopModsContext> _mockContext;
    private readonly Mock<IWorkshopModsProcessingService> _mockProcessingService;

    public InstallOperationTests()
    {
        _mockContext = new Mock<IWorkshopModsContext>();
        _mockProcessingService = new Mock<IWorkshopModsProcessingService>();
        var mockLogger = new Mock<IUksfLogger>();
        _installOperation = new InstallOperation(_mockContext.Object, _mockProcessingService.Object, mockLogger.Object);
    }

    [Fact]
    public async Task CheckAsync_WhenGetModFilesFails_ShouldReturnFailure()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = WorkshopModId,
            Name = "Test Mod"
        };
        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _mockProcessingService.Setup(x => x.GetWorkshopModPath(WorkshopModId)).Returns("/path/to/mod");
        _mockProcessingService.Setup(x => x.GetModFiles("/path/to/mod")).Throws(new InvalidOperationException("Duplicate PBOs found"));

        // Act
        var result = await _installOperation.CheckAsync(WorkshopModId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Duplicate PBOs found");
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CheckAsync_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = WorkshopModId,
            Name = "Test Mod"
        };
        var pbos = new List<string> { "mod1.pbo", "mod2.pbo" };

        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _mockProcessingService.Setup(x => x.GetWorkshopModPath(WorkshopModId)).Returns("/path/to/mod");
        _mockProcessingService.Setup(x => x.GetModFiles("/path/to/mod")).Returns(pbos);

        // Act
        var result = await _installOperation.CheckAsync(WorkshopModId);

        // Assert
        result.Success.Should().BeTrue();
        result.InterventionRequired.Should().BeTrue();
        _mockProcessingService.Verify(x => x.SetAvailablePbos(workshopMod, pbos), Times.Once);
    }

    [Fact]
    public async Task DownloadAsync_WhenCancelled_ShouldThrowOperationCancelledException()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = WorkshopModId,
            Name = "Test Mod"
        };
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _mockProcessingService.Setup(x => x.DownloadWithRetries(WorkshopModId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _installOperation.DownloadAsync(WorkshopModId, cancellationTokenSource.Token));
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Download cancelled"), Times.Once);
    }

    [Fact]
    public async Task DownloadAsync_WhenDownloadFails_ShouldReturnFailure()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = WorkshopModId,
            Name = "Test Mod"
        };
        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _mockProcessingService.Setup(x => x.DownloadWithRetries(WorkshopModId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new Exception("Download failed"));

        // Act
        var result = await _installOperation.DownloadAsync(WorkshopModId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Download failed");
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DownloadAsync_WithValidWorkshopModId_ShouldSucceed()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = WorkshopModId,
            Name = "Test Mod"
        };
        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _mockProcessingService.Setup(x => x.DownloadWithRetries(WorkshopModId, It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var result = await _installOperation.DownloadAsync(WorkshopModId);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Installing, "Downloading..."), Times.Once);
        _mockProcessingService.Verify(x => x.DownloadWithRetries(WorkshopModId, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallAsync_WhenCopyFails_ShouldReturnFailure()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = WorkshopModId,
            Name = "Test Mod"
        };
        var selectedPbos = new List<string> { "mod1.pbo" };

        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _mockProcessingService.Setup(x => x.CopyPbosToDependencies(workshopMod, selectedPbos, It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new IOException("Copy failed"));

        // Act
        var result = await _installOperation.InstallAsync(WorkshopModId, selectedPbos);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Copy failed");
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task InstallAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = WorkshopModId,
            Name = "Test Mod"
        };
        var selectedPbos = new List<string> { "mod1.pbo" };
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _mockProcessingService.Setup(x => x.CopyPbosToDependencies(workshopMod, selectedPbos, It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _installOperation.InstallAsync(WorkshopModId, selectedPbos, cancellationTokenSource.Token));
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Installation cancelled"), Times.Once);
    }

    [Fact]
    public async Task InstallAsync_WithValidData_ShouldSucceed()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = WorkshopModId,
            Name = "Test Mod"
        };
        var selectedPbos = new List<string> { "mod1.pbo", "mod2.pbo" };

        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _mockProcessingService.Setup(x => x.CopyPbosToDependencies(workshopMod, selectedPbos, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        // Act
        var result = await _installOperation.InstallAsync(WorkshopModId, selectedPbos);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        workshopMod.Pbos.Should().BeEquivalentTo(selectedPbos);
        workshopMod.Status.Should().Be(WorkshopModStatus.InstalledPendingRelease);
        workshopMod.ErrorMessage.Should().BeNull();
        _mockContext.Verify(x => x.Replace(workshopMod), Times.Once);
    }
}
