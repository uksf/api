using FluentAssertions;
using Moq;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;
using Xunit;

namespace UKSF.Api.Modpack.Tests.WorkshopModProcessing;

public class UpdateOperationTests
{
    private readonly Mock<IWorkshopModsContext> _mockContext;
    private readonly Mock<IWorkshopModsProcessingService> _mockProcessingService;
    private readonly UpdateOperation _operation;

    public UpdateOperationTests()
    {
        _mockContext = new Mock<IWorkshopModsContext>();
        _mockProcessingService = new Mock<IWorkshopModsProcessingService>();
        _operation = new UpdateOperation(_mockContext.Object, _mockProcessingService.Object);
    }

    private DomainWorkshopMod SetupWorkshopMod(
        string workshopModId = "test-mod-123",
        bool rootMod = false,
        WorkshopModStatus status = WorkshopModStatus.Updating,
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

    // Download tests

    [Fact]
    public async Task DownloadAsync_WithValidWorkshopMod_ShouldSucceed()
    {
        var workshopMod = SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.DownloadWithRetries("test-mod-123", It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _operation.DownloadAsync("test-mod-123");

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Updating, "Downloading..."), Times.Once);
        _mockProcessingService.Verify(x => x.DownloadWithRetries("test-mod-123", It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadAsync_WhenDownloadFails_ShouldReturnFailure()
    {
        SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.DownloadWithRetries("test-mod-123", It.IsAny<int>(), It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new Exception("Download failed"));

        var result = await _operation.DownloadAsync("test-mod-123");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Download failed");
    }

    [Fact]
    public async Task DownloadAsync_WhenDownloadFails_ShouldNotUpdateStatusToError()
    {
        SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.DownloadWithRetries("test-mod-123", It.IsAny<int>(), It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new Exception("Download failed"));

        await _operation.DownloadAsync("test-mod-123");

        _mockProcessingService.Verify(x => x.UpdateModStatus(It.IsAny<DomainWorkshopMod>(), WorkshopModStatus.Error, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DownloadAsync_WhenCancelled_ShouldThrowOperationCancelledException()
    {
        var workshopMod = SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.DownloadWithRetries("test-mod-123", It.IsAny<int>(), It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new OperationCanceledException());
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _operation.DownloadAsync("test-mod-123", cancellationTokenSource.Token));
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Update download cancelled"), Times.Once);
    }

    [Fact]
    public async Task DownloadAsync_WhenModNotFound_ShouldReturnFailure()
    {
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>())).Returns((DomainWorkshopMod)null);

        var result = await _operation.DownloadAsync("missing-mod");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    // Check tests

    [Fact]
    public async Task CheckAsync_WithPbosChanged_ShouldRequireIntervention()
    {
        var workshopMod = SetupWorkshopMod();
        var pbos = new List<string> { "mod1.pbo", "mod2.pbo" };
        _mockProcessingService.Setup(x => x.GetWorkshopModPath("test-mod-123")).Returns("/path/to/mod");
        _mockProcessingService.Setup(x => x.GetModFiles("/path/to/mod")).Returns(pbos);

        var result = await _operation.CheckAsync("test-mod-123");

        result.Success.Should().BeTrue();
        result.InterventionRequired.Should().BeTrue();
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Updating, "Checking..."), Times.Once);
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.InterventionRequired, "Select PBOs to install"), Times.Once);
        _mockProcessingService.Verify(x => x.SetAvailablePbos(workshopMod, pbos), Times.Once);
    }

    [Fact]
    public async Task CheckAsync_WithPbosUnchanged_ShouldNotRequireIntervention()
    {
        var pbos = new List<string> { "mod1.pbo", "mod2.pbo" };
        SetupWorkshopMod(pbos: pbos);
        _mockProcessingService.Setup(x => x.GetWorkshopModPath("test-mod-123")).Returns("/path/to/mod");
        _mockProcessingService.Setup(x => x.GetModFiles("/path/to/mod")).Returns(pbos);

        var result = await _operation.CheckAsync("test-mod-123");

        result.Success.Should().BeTrue();
        result.InterventionRequired.Should().BeFalse();
        _mockProcessingService.Verify(
            x => x.UpdateModStatus(It.IsAny<DomainWorkshopMod>(), WorkshopModStatus.InterventionRequired, It.IsAny<string>()),
            Times.Never
        );
        _mockProcessingService.Verify(x => x.SetAvailablePbos(It.IsAny<DomainWorkshopMod>(), pbos), Times.Once);
    }

    [Fact]
    public async Task CheckAsync_ShouldReturnAvailablePbos()
    {
        var pbos = new List<string> { "mod1.pbo", "mod2.pbo" };
        SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.GetWorkshopModPath("test-mod-123")).Returns("/path/to/mod");
        _mockProcessingService.Setup(x => x.GetModFiles("/path/to/mod")).Returns(pbos);

        var result = await _operation.CheckAsync("test-mod-123");

        result.AvailablePbos.Should().BeEquivalentTo(pbos);
    }

    [Fact]
    public async Task CheckAsync_ForRootMod_ShouldReturnNoInterventionRequired()
    {
        SetupWorkshopMod(rootMod: true);

        var result = await _operation.CheckAsync("test-mod-123");

        result.Success.Should().BeTrue();
        result.InterventionRequired.Should().BeFalse();
        result.AvailablePbos.Should().BeNull();
        _mockProcessingService.Verify(x => x.GetModFiles(It.IsAny<string>()), Times.Never);
        _mockProcessingService.Verify(x => x.SetAvailablePbos(It.IsAny<DomainWorkshopMod>(), It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task CheckAsync_WhenGetModFilesFails_ShouldReturnFailure()
    {
        SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.GetWorkshopModPath("test-mod-123")).Returns("/path/to/mod");
        _mockProcessingService.Setup(x => x.GetModFiles("/path/to/mod")).Throws(new InvalidOperationException("Duplicate PBOs found"));

        var result = await _operation.CheckAsync("test-mod-123");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Duplicate PBOs found");
    }

    [Fact]
    public async Task CheckAsync_WhenGetModFilesFails_ShouldNotUpdateStatusToError()
    {
        SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.GetWorkshopModPath("test-mod-123")).Returns("/path/to/mod");
        _mockProcessingService.Setup(x => x.GetModFiles("/path/to/mod")).Throws(new InvalidOperationException("Duplicate PBOs found"));

        await _operation.CheckAsync("test-mod-123");

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

    // Execute tests

    [Fact]
    public async Task ExecuteAsync_WithRootMod_ShouldDeleteThenCopy()
    {
        var workshopMod = SetupWorkshopMod(rootMod: true);
        _mockProcessingService.Setup(x => x.CopyRootModToRepos(workshopMod, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        var result = await _operation.ExecuteAsync("test-mod-123", []);

        result.Success.Should().BeTrue();
        _mockProcessingService.Verify(x => x.DeleteRootModFromRepos(workshopMod), Times.Once);
        _mockProcessingService.Verify(x => x.CopyRootModToRepos(workshopMod, It.IsAny<CancellationToken>()), Times.Once);
        _mockProcessingService.Verify(
            x => x.CopyPbosToDependencies(It.IsAny<DomainWorkshopMod>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        workshopMod.Status.Should().Be(WorkshopModStatus.UpdatedPendingRelease);
    }

    [Fact]
    public async Task ExecuteAsync_WithRootMod_ShouldDeleteBeforeCopy()
    {
        var workshopMod = SetupWorkshopMod(rootMod: true);
        var callOrder = new List<string>();
        _mockProcessingService.Setup(x => x.DeleteRootModFromRepos(workshopMod)).Callback(() => callOrder.Add("delete"));
        _mockProcessingService.Setup(x => x.CopyRootModToRepos(workshopMod, It.IsAny<CancellationToken>()))
                              .Callback(() => callOrder.Add("copy"))
                              .Returns(Task.CompletedTask);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        await _operation.ExecuteAsync("test-mod-123", []);

        callOrder.Should().ContainInOrder("delete", "copy");
    }

    [Fact]
    public async Task ExecuteAsync_WithPboMod_ShouldCopyThenDeleteRemovedPbos()
    {
        var oldPbos = new List<string>
        {
            "old1.pbo",
            "old2.pbo",
            "kept.pbo"
        };
        var workshopMod = SetupWorkshopMod(pbos: oldPbos);
        var selectedPbos = new List<string> { "kept.pbo", "new1.pbo" };
        _mockProcessingService.Setup(x => x.CopyPbosToDependencies(workshopMod, selectedPbos, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        var result = await _operation.ExecuteAsync("test-mod-123", selectedPbos);

        result.Success.Should().BeTrue();
        _mockProcessingService.Verify(x => x.CopyPbosToDependencies(workshopMod, selectedPbos, It.IsAny<CancellationToken>()), Times.Once);
        _mockProcessingService.Verify(
            x => x.DeletePbosFromDependencies(It.Is<List<string>>(pbos => pbos.Contains("old1.pbo") && pbos.Contains("old2.pbo") && pbos.Count == 2)),
            Times.Once
        );
        workshopMod.Pbos.Should().BeEquivalentTo(selectedPbos);
    }

    [Fact]
    public async Task ExecuteAsync_WithPboMod_WhenNoRemovedPbos_ShouldSkipDelete()
    {
        var oldPbos = new List<string> { "mod1.pbo" };
        var workshopMod = SetupWorkshopMod(pbos: oldPbos);
        var selectedPbos = new List<string> { "mod1.pbo", "mod2.pbo" };
        _mockProcessingService.Setup(x => x.CopyPbosToDependencies(workshopMod, selectedPbos, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        await _operation.ExecuteAsync("test-mod-123", selectedPbos);

        _mockProcessingService.Verify(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithPboMod_WhenOldPbosEmpty_ShouldSkipDelete()
    {
        var workshopMod = SetupWorkshopMod(pbos: []);
        var selectedPbos = new List<string> { "mod1.pbo" };
        _mockProcessingService.Setup(x => x.CopyPbosToDependencies(workshopMod, selectedPbos, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        await _operation.ExecuteAsync("test-mod-123", selectedPbos);

        _mockProcessingService.Verify(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSetUpdatedPendingReleaseStatus()
    {
        var workshopMod = SetupWorkshopMod();
        var selectedPbos = new List<string> { "mod1.pbo" };
        _mockProcessingService.Setup(x => x.CopyPbosToDependencies(workshopMod, selectedPbos, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        await _operation.ExecuteAsync("test-mod-123", selectedPbos);

        workshopMod.Status.Should().Be(WorkshopModStatus.UpdatedPendingRelease);
        workshopMod.StatusMessage.Should().Be("Updated pending next modpack release");
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Updating, "Updating..."), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSetLastUpdatedLocallyAndClearErrorMessage()
    {
        var workshopMod = SetupWorkshopMod();
        workshopMod.ErrorMessage = "Previous error";
        var selectedPbos = new List<string> { "mod1.pbo" };
        _mockProcessingService.Setup(x => x.CopyPbosToDependencies(workshopMod, selectedPbos, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        await _operation.ExecuteAsync("test-mod-123", selectedPbos);

        workshopMod.LastUpdatedLocally.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        workshopMod.ErrorMessage.Should().BeNull();
        _mockContext.Verify(x => x.Replace(workshopMod), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCopyFails_ShouldReturnFailure()
    {
        var workshopMod = SetupWorkshopMod();
        var selectedPbos = new List<string> { "mod1.pbo" };
        _mockProcessingService.Setup(x => x.CopyPbosToDependencies(workshopMod, selectedPbos, It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new IOException("Copy failed"));

        var result = await _operation.ExecuteAsync("test-mod-123", selectedPbos);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Copy failed");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCopyFails_ShouldNotUpdateStatusToError()
    {
        var workshopMod = SetupWorkshopMod();
        var selectedPbos = new List<string> { "mod1.pbo" };
        _mockProcessingService.Setup(x => x.CopyPbosToDependencies(workshopMod, selectedPbos, It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new IOException("Copy failed"));

        await _operation.ExecuteAsync("test-mod-123", selectedPbos);

        _mockProcessingService.Verify(x => x.UpdateModStatus(It.IsAny<DomainWorkshopMod>(), WorkshopModStatus.Error, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ShouldThrow()
    {
        var workshopMod = SetupWorkshopMod();
        var selectedPbos = new List<string> { "mod1.pbo" };
        _mockProcessingService.Setup(x => x.CopyPbosToDependencies(workshopMod, selectedPbos, It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new OperationCanceledException());
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _operation.ExecuteAsync("test-mod-123", selectedPbos, cancellationTokenSource.Token));
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Update cancelled"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_RootMod_WhenCopyFails_ShouldReturnFailure()
    {
        var workshopMod = SetupWorkshopMod(rootMod: true);
        _mockProcessingService.Setup(x => x.CopyRootModToRepos(workshopMod, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("Copy failed"));

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
