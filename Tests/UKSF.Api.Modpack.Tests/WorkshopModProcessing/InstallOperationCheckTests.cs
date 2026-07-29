using FluentAssertions;
using Moq;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;
using Xunit;

namespace UKSF.Api.Modpack.Tests.WorkshopModProcessing;

public class InstallOperationCheckTests
{
    private readonly Mock<IWorkshopModsContext> _mockContext = new();
    private readonly Mock<IWorkshopModsProcessingService> _mockProcessingService = new();
    private readonly Mock<IWorkshopModDependencyFilesService> _mockDependencyFilesService = new();
    private readonly Mock<IWorkshopModRootFilesService> _mockRootFilesService = new();
    private readonly InstallOperation _operation;

    public InstallOperationCheckTests()
    {
        _mockProcessingService.Setup(x => x.GetWorkshopModPath("test-mod-123")).Returns("/path/to/mod");
        _mockProcessingService.Setup(x => x.GetExtensions(It.IsAny<string>())).Returns([]);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);
        _operation = new InstallOperation(_mockContext.Object, _mockProcessingService.Object, _mockDependencyFilesService.Object, _mockRootFilesService.Object);
    }

    private DomainWorkshopMod SetupWorkshopMod(bool rootMod = false, List<string> pbos = null, List<string> files = null)
    {
        var workshopMod = new DomainWorkshopMod
        {
            Id = "test-mod-123",
            SteamId = "test-mod-123",
            Name = "Test Mod",
            RootMod = rootMod,
            Status = WorkshopModStatus.Installing,
            Pbos = pbos ?? [],
            Extensions = files ?? []
        };
        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        return workshopMod;
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
        _mockProcessingService.Verify(x => x.SetAvailable(workshopMod, pbos, It.IsAny<List<string>>()), Times.Once);
    }

    [Fact]
    public async Task CheckAsync_WithPbosUnchanged_ShouldNotRequireIntervention()
    {
        var pbos = new List<string> { "mod1.pbo", "mod2.pbo" };
        SetupWorkshopMod(pbos: pbos, files: []);
        _mockProcessingService.Setup(x => x.GetPboFiles("/path/to/mod")).Returns(pbos);

        var result = await _operation.CheckAsync("test-mod-123");

        result.Success.Should().BeTrue();
        result.InterventionRequired.Should().BeFalse();
        result.AvailablePbos.Should().BeEquivalentTo(pbos);
        _mockProcessingService.Verify(
            x => x.UpdateModStatus(It.IsAny<DomainWorkshopMod>(), WorkshopModStatus.InterventionRequired, It.IsAny<string>()),
            Times.Never
        );
        _mockProcessingService.Verify(x => x.SetAvailable(It.IsAny<DomainWorkshopMod>(), pbos, It.IsAny<List<string>>()), Times.Once);
    }

    [Fact]
    public async Task CheckAsync_WithFilesAndNoPbos_ShouldOfferTheFilesForSelection()
    {
        var workshopMod = SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.GetPboFiles("/path/to/mod")).Returns([]);
        _mockProcessingService.Setup(x => x.GetExtensions("/path/to/mod")).Returns(["ctab_connect.dll"]);

        var result = await _operation.CheckAsync("test-mod-123");

        result.Success.Should().BeTrue();
        result.InterventionRequired.Should().BeTrue();
        result.AvailablePbos.Should().BeEmpty();
        result.AvailableExtensions.Should().BeEquivalentTo("ctab_connect.dll");
        _mockProcessingService.Verify(
            x => x.SetAvailable(workshopMod, It.IsAny<List<string>>(), It.Is<List<string>>(files => files.Single() == "ctab_connect.dll")),
            Times.Once
        );
    }

    [Fact]
    public async Task CheckAsync_WhenOnlyFilesChanged_ShouldRequireIntervention()
    {
        var pbos = new List<string> { "mod1.pbo" };
        SetupWorkshopMod(pbos: pbos, files: ["old_extension.dll"]);
        _mockProcessingService.Setup(x => x.GetPboFiles("/path/to/mod")).Returns(pbos);
        _mockProcessingService.Setup(x => x.GetExtensions("/path/to/mod")).Returns(["new_extension.dll"]);

        var result = await _operation.CheckAsync("test-mod-123");

        result.InterventionRequired.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_WhenPbosAndFilesUnchanged_ShouldNotRequireIntervention()
    {
        var pbos = new List<string> { "mod1.pbo" };
        SetupWorkshopMod(pbos: pbos, files: ["extension.dll"]);
        _mockProcessingService.Setup(x => x.GetPboFiles("/path/to/mod")).Returns(pbos);
        _mockProcessingService.Setup(x => x.GetExtensions("/path/to/mod")).Returns(["extension.dll"]);

        var result = await _operation.CheckAsync("test-mod-123");

        result.InterventionRequired.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_WithNoPbosAndNoExtensions_ShouldReturnFailure()
    {
        SetupWorkshopMod();
        _mockProcessingService.Setup(x => x.GetPboFiles("/path/to/mod")).Returns([]);

        var result = await _operation.CheckAsync("test-mod-123");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No PBOs or extensions found");
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
        _mockProcessingService.Verify(x => x.SetAvailable(It.IsAny<DomainWorkshopMod>(), It.IsAny<List<string>>(), It.IsAny<List<string>>()), Times.Never);
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
}
