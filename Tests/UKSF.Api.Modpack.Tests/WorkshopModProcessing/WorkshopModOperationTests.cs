using FluentAssertions;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;
using Xunit;

namespace UKSF.Api.Modpack.Tests.WorkshopModProcessing;

public class WorkshopModOperationTests
{
    private readonly Mock<IWorkshopModsContext> _mockContext;
    private readonly Mock<IWorkshopModsProcessingService> _mockProcessingService;
    private readonly WorkshopModOperation _operation;

    public WorkshopModOperationTests()
    {
        _mockContext = new Mock<IWorkshopModsContext>();
        _mockProcessingService = new Mock<IWorkshopModsProcessingService>();
        var mockLogger = new Mock<IUksfLogger>();
        _operation = new WorkshopModOperation(_mockContext.Object, _mockProcessingService.Object, mockLogger.Object);
    }

    private DomainWorkshopMod SetupWorkshopMod(
        string workshopModId = "test-mod-123",
        bool rootMod = false,
        WorkshopModStatus status = WorkshopModStatus.Installing,
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

    [Theory]
    [InlineData(WorkshopModOperationType.Install, WorkshopModStatus.Installing)]
    [InlineData(WorkshopModOperationType.Update, WorkshopModStatus.Updating)]
    public async Task DownloadAsync_WithValidWorkshopMod_ShouldSucceed(WorkshopModOperationType type, WorkshopModStatus expectedStatus)
    {
        var workshopMod = SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.DownloadWithRetries("test-mod-123", It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _operation.DownloadAsync("test-mod-123", type);

        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, expectedStatus, "Downloading..."), Times.Once);
        _mockProcessingService.Verify(x => x.DownloadWithRetries("test-mod-123", It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(WorkshopModOperationType.Install)]
    [InlineData(WorkshopModOperationType.Update)]
    public async Task DownloadAsync_WhenDownloadFails_ShouldReturnFailure(WorkshopModOperationType type)
    {
        SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.DownloadWithRetries("test-mod-123", It.IsAny<int>(), It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new Exception("Download failed"));

        var result = await _operation.DownloadAsync("test-mod-123", type);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Download failed");
    }

    [Fact]
    public async Task DownloadAsync_WhenCancelled_ShouldThrowOperationCancelledException()
    {
        var workshopMod = SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.DownloadWithRetries("test-mod-123", It.IsAny<int>(), It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new OperationCanceledException());
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _operation.DownloadAsync("test-mod-123", WorkshopModOperationType.Install, cts.Token));
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "Download cancelled"), Times.Once);
    }

    [Fact]
    public async Task DownloadAsync_WhenModNotFound_ShouldReturnFailure()
    {
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>())).Returns((DomainWorkshopMod)null);

        var result = await _operation.DownloadAsync("missing-mod", WorkshopModOperationType.Install);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    // Check tests

    [Theory]
    [InlineData(WorkshopModOperationType.Install, WorkshopModStatus.Installing)]
    [InlineData(WorkshopModOperationType.Update, WorkshopModStatus.Updating)]
    public async Task CheckAsync_WithPbosChanged_ShouldRequireIntervention(WorkshopModOperationType type, WorkshopModStatus expectedStatus)
    {
        var workshopMod = SetupWorkshopMod();
        var pbos = new List<string> { "mod1.pbo", "mod2.pbo" };
        _mockProcessingService.Setup(x => x.GetWorkshopModPath("test-mod-123")).Returns("/path/to/mod");
        _mockProcessingService.Setup(x => x.GetModFiles("/path/to/mod")).Returns(pbos);

        var result = await _operation.CheckAsync("test-mod-123", type);

        result.Success.Should().BeTrue();
        result.InterventionRequired.Should().BeTrue();
        _mockProcessingService.Verify(x => x.UpdateModStatus(workshopMod, expectedStatus, "Checking..."), Times.Once);
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

        var result = await _operation.CheckAsync("test-mod-123", WorkshopModOperationType.Update);

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

        var result = await _operation.CheckAsync("test-mod-123", WorkshopModOperationType.Install);

        result.AvailablePbos.Should().BeEquivalentTo(pbos);
    }

    [Fact]
    public async Task CheckAsync_WithPbosUnchanged_ShouldReturnAvailablePbos()
    {
        var pbos = new List<string> { "mod1.pbo", "mod2.pbo" };
        SetupWorkshopMod(pbos: pbos);
        _mockProcessingService.Setup(x => x.GetWorkshopModPath("test-mod-123")).Returns("/path/to/mod");
        _mockProcessingService.Setup(x => x.GetModFiles("/path/to/mod")).Returns(pbos);

        var result = await _operation.CheckAsync("test-mod-123", WorkshopModOperationType.Update);

        result.AvailablePbos.Should().BeEquivalentTo(pbos);
    }

    [Fact]
    public async Task CheckAsync_ForRootMod_ShouldReturnNullAvailablePbos()
    {
        SetupWorkshopMod(rootMod: true);

        var result = await _operation.CheckAsync("test-mod-123", WorkshopModOperationType.Install);

        result.AvailablePbos.Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_ForRootMod_ShouldReturnNoInterventionRequired()
    {
        SetupWorkshopMod(rootMod: true);

        var result = await _operation.CheckAsync("test-mod-123", WorkshopModOperationType.Install);

        result.Success.Should().BeTrue();
        result.InterventionRequired.Should().BeFalse();
        _mockProcessingService.Verify(x => x.GetModFiles(It.IsAny<string>()), Times.Never);
        _mockProcessingService.Verify(x => x.SetAvailablePbos(It.IsAny<DomainWorkshopMod>(), It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task CheckAsync_WhenGetModFilesFails_ShouldReturnFailure()
    {
        SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.GetWorkshopModPath("test-mod-123")).Returns("/path/to/mod");
        _mockProcessingService.Setup(x => x.GetModFiles("/path/to/mod")).Throws(new InvalidOperationException("Duplicate PBOs found"));

        var result = await _operation.CheckAsync("test-mod-123", WorkshopModOperationType.Install);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Duplicate PBOs found");
    }

    [Fact]
    public async Task CheckAsync_WhenModNotFound_ShouldReturnFailure()
    {
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>())).Returns((DomainWorkshopMod)null);

        var result = await _operation.CheckAsync("missing-mod", WorkshopModOperationType.Install);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    // Execute tests - Install path

    [Fact]
    public async Task ExecuteAsync_Install_WithPboMod_ShouldCopyPbos()
    {
        var workshopMod = SetupWorkshopMod();
        var selectedPbos = new List<string> { "mod1.pbo", "mod2.pbo" };
        _mockProcessingService.Setup(x => x.CopyPbosToDependencies(workshopMod, selectedPbos, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        var result = await _operation.ExecuteAsync("test-mod-123", WorkshopModOperationType.Install, selectedPbos);

        result.Success.Should().BeTrue();
        workshopMod.Pbos.Should().BeEquivalentTo(selectedPbos);
        workshopMod.Status.Should().Be(WorkshopModStatus.InstalledPendingRelease);
        workshopMod.ErrorMessage.Should().BeNull();
        _mockContext.Verify(x => x.Replace(workshopMod), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Install_WithRootMod_ShouldCopyRootMod()
    {
        var workshopMod = SetupWorkshopMod(rootMod: true);
        _mockProcessingService.Setup(x => x.CopyRootModToRepos(workshopMod, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        var result = await _operation.ExecuteAsync("test-mod-123", WorkshopModOperationType.Install, []);

        result.Success.Should().BeTrue();
        _mockProcessingService.Verify(x => x.CopyRootModToRepos(workshopMod, It.IsAny<CancellationToken>()), Times.Once);
        _mockProcessingService.Verify(
            x => x.CopyPbosToDependencies(It.IsAny<DomainWorkshopMod>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        workshopMod.Status.Should().Be(WorkshopModStatus.InstalledPendingRelease);
    }

    [Fact]
    public async Task ExecuteAsync_Install_WhenCopyFails_ShouldReturnFailure()
    {
        var workshopMod = SetupWorkshopMod();
        var selectedPbos = new List<string> { "mod1.pbo" };
        _mockProcessingService.Setup(x => x.CopyPbosToDependencies(workshopMod, selectedPbos, It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new IOException("Copy failed"));

        var result = await _operation.ExecuteAsync("test-mod-123", WorkshopModOperationType.Install, selectedPbos);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Copy failed");
    }

    [Fact]
    public async Task ExecuteAsync_Install_WhenCancelled_ShouldThrow()
    {
        var workshopMod = SetupWorkshopMod();
        var selectedPbos = new List<string> { "mod1.pbo" };
        _mockProcessingService.Setup(x => x.CopyPbosToDependencies(workshopMod, selectedPbos, It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new OperationCanceledException());
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _operation.ExecuteAsync(
                                                                 "test-mod-123",
                                                                 WorkshopModOperationType.Install,
                                                                 selectedPbos,
                                                                 cts.Token
                                                             )
        );
    }

    [Fact]
    public async Task ExecuteAsync_Install_RootMod_WhenCopyFails_ShouldReturnFailure()
    {
        var workshopMod = SetupWorkshopMod(rootMod: true);
        _mockProcessingService.Setup(x => x.CopyRootModToRepos(workshopMod, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("Copy failed"));

        var result = await _operation.ExecuteAsync("test-mod-123", WorkshopModOperationType.Install, []);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Copy failed");
    }

    // Execute tests - Update path

    [Fact]
    public async Task ExecuteAsync_Update_WithPboMod_ShouldCopyNewAndDeleteOld()
    {
        var oldPbos = new List<string> { "old1.pbo", "old2.pbo" };
        var newPbos = new List<string> { "new1.pbo", "new2.pbo" };
        var workshopMod = SetupWorkshopMod(status: WorkshopModStatus.Installed, pbos: oldPbos);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        var result = await _operation.ExecuteAsync("test-mod-123", WorkshopModOperationType.Update, newPbos);

        result.Success.Should().BeTrue();
        _mockProcessingService.Verify(x => x.CopyPbosToDependencies(workshopMod, newPbos, It.IsAny<CancellationToken>()), Times.Once);
        _mockProcessingService.Verify(x => x.DeletePbosFromDependencies(oldPbos), Times.Once);
        workshopMod.Pbos.Should().BeEquivalentTo(newPbos);
        workshopMod.Status.Should().Be(WorkshopModStatus.UpdatedPendingRelease);
    }

    [Fact]
    public async Task ExecuteAsync_Update_WithEmptyOldPbos_ShouldNotAttemptDelete()
    {
        var newPbos = new List<string> { "new1.pbo" };
        var workshopMod = SetupWorkshopMod(status: WorkshopModStatus.Installing, pbos: []);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);

        var result = await _operation.ExecuteAsync("test-mod-123", WorkshopModOperationType.Update, newPbos);

        result.Success.Should().BeTrue();
        _mockProcessingService.Verify(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_Update_WithRootMod_ShouldDeleteAndRecopy()
    {
        var workshopMod = SetupWorkshopMod(rootMod: true, status: WorkshopModStatus.Installed);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);
        _mockProcessingService.Setup(x => x.CopyRootModToRepos(workshopMod, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _operation.ExecuteAsync("test-mod-123", WorkshopModOperationType.Update, []);

        result.Success.Should().BeTrue();
        _mockProcessingService.Verify(x => x.DeleteRootModFromRepos(workshopMod), Times.Once);
        _mockProcessingService.Verify(x => x.CopyRootModToRepos(workshopMod, It.IsAny<CancellationToken>()), Times.Once);
        _mockProcessingService.Verify(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>()), Times.Never);
        _mockProcessingService.Verify(
            x => x.CopyPbosToDependencies(It.IsAny<DomainWorkshopMod>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        workshopMod.Status.Should().Be(WorkshopModStatus.UpdatedPendingRelease);
    }

    [Fact]
    public async Task ExecuteAsync_Update_WhenCopyFails_ShouldReturnFailure()
    {
        var workshopMod = SetupWorkshopMod(status: WorkshopModStatus.Installed, pbos: ["old.pbo"]);
        _mockProcessingService.Setup(x => x.CopyPbosToDependencies(workshopMod, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new IOException("Copy failed"));

        var result = await _operation.ExecuteAsync("test-mod-123", WorkshopModOperationType.Update, ["new.pbo"]);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Copy failed");
    }

    [Fact]
    public async Task ExecuteAsync_Update_WhenCancelled_ShouldThrow()
    {
        var workshopMod = SetupWorkshopMod(status: WorkshopModStatus.Installed, pbos: ["old.pbo"]);
        _mockProcessingService.Setup(x => x.CopyPbosToDependencies(workshopMod, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new OperationCanceledException());
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _operation.ExecuteAsync(
                                                                 "test-mod-123",
                                                                 WorkshopModOperationType.Update,
                                                                 ["new.pbo"],
                                                                 cts.Token
                                                             )
        );
    }

    [Fact]
    public async Task ExecuteAsync_Update_RootMod_WhenCopyFails_ShouldReturnFailure()
    {
        var workshopMod = SetupWorkshopMod(rootMod: true, status: WorkshopModStatus.Installed);
        _mockProcessingService.Setup(x => x.CopyRootModToRepos(workshopMod, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("Copy failed"));

        var result = await _operation.ExecuteAsync("test-mod-123", WorkshopModOperationType.Update, []);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Copy failed");
    }

    [Fact]
    public async Task ExecuteAsync_WhenModNotFound_ShouldReturnFailure()
    {
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainWorkshopMod, bool>>())).Returns((DomainWorkshopMod)null);

        var result = await _operation.ExecuteAsync("missing-mod", WorkshopModOperationType.Install, []);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }
}
