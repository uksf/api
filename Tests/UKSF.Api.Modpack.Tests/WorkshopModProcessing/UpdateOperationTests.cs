using FluentAssertions;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;
using Xunit;

namespace UKSF.Api.Modpack.Tests.WorkshopModProcessing;

public class UpdateOperationTests
{
    private readonly Mock<IWorkshopModsContext> _mockContext;
    private readonly Mock<IWorkshopModsProcessingService> _mockProcessingService;
    private readonly UpdateOperation _updateOperation;

    public UpdateOperationTests()
    {
        _mockContext = new Mock<IWorkshopModsContext>();
        _mockProcessingService = new Mock<IWorkshopModsProcessingService>();
        var mockLogger = new Mock<IUksfLogger>();
        _updateOperation = new UpdateOperation(_mockContext.Object, _mockProcessingService.Object, mockLogger.Object);
    }

    [Fact]
    public async Task DownloadAsync_WithValidWorkshopMod_ShouldSucceed()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = "123456",
            Status = WorkshopModStatus.Installed
        };
        _mockContext.Setup(x => x.GetSingle(WorkshopModId)).Returns(workshopMod);

        // Act
        var result = await _updateOperation.DownloadAsync(WorkshopModId);

        // Assert
        result.Success.Should().BeTrue();
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Updating, "Downloading..."), Times.Once);
        _mockProcessingService.Verify(x => x.DownloadWithRetries(WorkshopModId, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadAsync_WhenDownloadFails_ShouldReturnFailure()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = "123456",
            Status = WorkshopModStatus.Installed
        };
        _mockContext.Setup(x => x.GetSingle(WorkshopModId)).Returns(workshopMod);
        _mockProcessingService.Setup(x => x.DownloadWithRetries(WorkshopModId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new Exception("Download failed"));

        // Act
        var result = await _updateOperation.DownloadAsync(WorkshopModId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Download failed");
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DownloadAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = "123456",
            Status = WorkshopModStatus.Installed
        };
        _mockContext.Setup(x => x.GetSingle(WorkshopModId)).Returns(workshopMod);
        _mockProcessingService.Setup(x => x.DownloadWithRetries(WorkshopModId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new OperationCanceledException());
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _updateOperation.DownloadAsync(WorkshopModId, cancellationTokenSource.Token));
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Download cancelled"), Times.Once);
    }

    [Fact]
    public async Task CheckAsync_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = "123456",
            Pbos = ["old.pbo"]
        };
        var newPbos = new List<string> { "new1.pbo", "new2.pbo" };

        _mockContext.Setup(x => x.GetSingle(WorkshopModId)).Returns(workshopMod);
        _mockProcessingService.Setup(x => x.GetWorkshopModPath("123456")).Returns("/path/to/mod");
        _mockProcessingService.Setup(x => x.GetModFiles("/path/to/mod")).Returns(newPbos);

        // Act
        var result = await _updateOperation.CheckAsync(WorkshopModId);

        // Assert
        result.Success.Should().BeTrue();
        result.AvailablePbos.Should().BeEquivalentTo(newPbos);
    }

    [Fact]
    public async Task CheckAsync_WhenGetModFilesFails_ShouldReturnFailure()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = "123456",
            Pbos = ["old.pbo"]
        };
        _mockContext.Setup(x => x.GetSingle(WorkshopModId)).Returns(workshopMod);
        _mockProcessingService.Setup(x => x.GetWorkshopModPath("123456")).Returns("/path/to/mod");
        _mockProcessingService.Setup(x => x.GetModFiles("/path/to/mod")).Throws(new InvalidOperationException("No PBO files"));

        // Act
        var result = await _updateOperation.CheckAsync(WorkshopModId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No PBO files");
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_ShouldDeleteOldPbosAndCopyNew()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var oldPbos = new List<string> { "old1.pbo", "old2.pbo" };
        var newPbos = new List<string> { "new1.pbo", "new2.pbo" };
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = "123456",
            Pbos = oldPbos,
            Status = WorkshopModStatus.Installed
        };

        _mockContext.Setup(x => x.GetSingle(WorkshopModId)).Returns(workshopMod);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        // Act
        var result = await _updateOperation.UpdateAsync(WorkshopModId, newPbos);

        // Assert
        result.Success.Should().BeTrue();
        _mockProcessingService.Verify(x => x.DeletePbosFromDependencies(oldPbos), Times.Once);
        _mockProcessingService.Verify(x => x.CopyPbosToDependencies(workshopMod, newPbos, It.IsAny<CancellationToken>()), Times.Once);
        workshopMod.Pbos.Should().BeEquivalentTo(newPbos);
        workshopMod.Status.Should().Be(WorkshopModStatus.UpdatedPendingRelease);
    }

    [Fact]
    public async Task UpdateAsync_WhenCopyFails_ShouldReturnFailure()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = "123456",
            Pbos = ["old.pbo"],
            Status = WorkshopModStatus.Installed
        };
        _mockContext.Setup(x => x.GetSingle(WorkshopModId)).Returns(workshopMod);
        _mockProcessingService.Setup(x => x.CopyPbosToDependencies(workshopMod, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new IOException("Copy failed"));

        // Act
        var result = await _updateOperation.UpdateAsync(WorkshopModId, ["new.pbo"]);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Copy failed");
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = "123456",
            Pbos = ["old.pbo"],
            Status = WorkshopModStatus.Installed
        };
        _mockContext.Setup(x => x.GetSingle(WorkshopModId)).Returns(workshopMod);
        _mockProcessingService.Setup(x => x.CopyPbosToDependencies(workshopMod, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new OperationCanceledException());
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _updateOperation.UpdateAsync(WorkshopModId, ["new.pbo"], cancellationTokenSource.Token));
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Update cancelled"), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithEmptyOldPbos_ShouldNotAttemptDelete()
    {
        // Arrange
        const string WorkshopModId = "test-mod-123";
        var newPbos = new List<string> { "new1.pbo" };
        var workshopMod = new DomainWorkshopMod
        {
            Id = WorkshopModId,
            SteamId = "123456",
            Pbos = [],
            Status = WorkshopModStatus.Installing
        };

        _mockContext.Setup(x => x.GetSingle(WorkshopModId)).Returns(workshopMod);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        // Act
        var result = await _updateOperation.UpdateAsync(WorkshopModId, newPbos);

        // Assert
        result.Success.Should().BeTrue();
        _mockProcessingService.Verify(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>()), Times.Never);
    }
}
