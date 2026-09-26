using FluentAssertions;
using Moq;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.WorkshopModProcessing.Operations;
using Xunit;

namespace UKSF.Api.Modpack.Tests.WorkshopModProcessing;

public class UnselectedFilesCleanupTests
{
    private readonly Mock<IWorkshopModsContext> _mockContext = new();
    private readonly Mock<IWorkshopModsProcessingService> _mockProcessingService = new();
    private readonly Mock<IWorkshopModDependencyFilesService> _mockDependencyFilesService = new();
    private readonly Mock<IWorkshopModRootFilesService> _mockRootFilesService = new();
    private readonly List<DomainWorkshopMod> _otherMods = [];
    private List<string> _deletedPbos = [];
    private List<string> _deletedExtensions = [];

    public UnselectedFilesCleanupTests()
    {
        _mockProcessingService.Setup(x => x.GetWorkshopModPath("test-mod-123")).Returns("/path/to/mod");
        _mockProcessingService.Setup(x => x.GetPboFiles("/path/to/mod")).Returns(["core.pbo", "compat_a.pbo", "compat_b.pbo"]);
        _mockProcessingService.Setup(x => x.GetExtensions("/path/to/mod")).Returns(["ext.dll", "helper.dll"]);
        _mockContext.Setup(x => x.Replace(It.IsAny<DomainWorkshopMod>())).Returns(Task.CompletedTask);
        _mockContext.Setup(x => x.Get(It.IsAny<Func<DomainWorkshopMod, bool>>()))
                    .Returns((Func<DomainWorkshopMod, bool> predicate) => _otherMods.Where(predicate));
        _mockDependencyFilesService.Setup(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>())).Callback<List<string>>(x => _deletedPbos = x);
        _mockDependencyFilesService.Setup(x => x.DeleteExtensionsFromDependencies(It.IsAny<List<string>>()))
                                   .Callback<List<string>>(x => _deletedExtensions = x);
    }

    private DomainWorkshopMod SetupWorkshopMod(List<string> pbos = null, List<string> extensions = null)
    {
        var workshopMod = new DomainWorkshopMod
        {
            Id = "test-mod-123",
            SteamId = "test-mod-123",
            Name = "Test Mod",
            Pbos = pbos ?? [],
            Extensions = extensions ?? []
        };
        _mockContext.Setup(x => x.GetSingle(It.Is<Func<DomainWorkshopMod, bool>>(predicate => predicate(workshopMod)))).Returns(workshopMod);
        _otherMods.Add(workshopMod);
        return workshopMod;
    }

    private void AddOtherMod(List<string> pbos = null, List<string> availablePbos = null, List<string> extensions = null)
    {
        _otherMods.Add(
            new DomainWorkshopMod
            {
                SteamId = $"other-{_otherMods.Count}",
                Pbos = pbos ?? [],
                AvailablePbos = availablePbos ?? [],
                Extensions = extensions ?? []
            }
        );
    }

    private UpdateOperation Update() =>
        new(_mockContext.Object, _mockProcessingService.Object, _mockDependencyFilesService.Object, _mockRootFilesService.Object);

    private InstallOperation Install() =>
        new(_mockContext.Object, _mockProcessingService.Object, _mockDependencyFilesService.Object, _mockRootFilesService.Object);

    [Fact]
    public async Task Update_ShouldDeleteUnselectedItemPbosThatWereNeverRecorded()
    {
        SetupWorkshopMod(pbos: ["core.pbo"]);

        var result = await Update().ExecuteAsync("test-mod-123", ["core.pbo"], []);

        result.Success.Should().BeTrue();
        _deletedPbos.Should().BeEquivalentTo("compat_a.pbo", "compat_b.pbo");
    }

    [Fact]
    public async Task Update_ShouldStillDeletePreviouslySelectedPbosNoLongerInTheItem()
    {
        SetupWorkshopMod(pbos: ["core.pbo", "retired.pbo"]);

        await Update().ExecuteAsync("test-mod-123", ["core.pbo", "compat_a.pbo", "compat_b.pbo"], []);

        _deletedPbos.Should().BeEquivalentTo("retired.pbo");
    }

    [Fact]
    public async Task Update_ShouldKeepPbosAnotherModListsAsItsOwnOrAvailable()
    {
        SetupWorkshopMod(pbos: ["core.pbo", "retired.pbo"]);
        AddOtherMod(pbos: ["COMPAT_A.pbo"]);
        AddOtherMod(availablePbos: ["retired.pbo"]);

        await Update().ExecuteAsync("test-mod-123", ["core.pbo"], []);

        _deletedPbos.Should().BeEquivalentTo("compat_b.pbo");
    }

    [Fact]
    public async Task Update_ShouldKeepPbosListedByAnotherDocumentWithTheSameSteamId()
    {
        SetupWorkshopMod(pbos: ["core.pbo"]);
        _otherMods.Add(
            new DomainWorkshopMod
            {
                Id = "duplicate",
                SteamId = "test-mod-123",
                Pbos = ["compat_a.pbo"]
            }
        );

        await Update().ExecuteAsync("test-mod-123", ["core.pbo"], []);

        _deletedPbos.Should().BeEquivalentTo("compat_b.pbo");
    }

    [Fact]
    public async Task Update_ShouldNotDeleteWhenEverythingUnselectedIsOwnedElsewhere()
    {
        SetupWorkshopMod(pbos: ["core.pbo"]);
        AddOtherMod(pbos: ["compat_a.pbo", "compat_b.pbo"], extensions: ["helper.dll"]);

        await Update().ExecuteAsync("test-mod-123", ["core.pbo"], ["ext.dll"]);

        _mockDependencyFilesService.Verify(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>()), Times.Never);
        _mockDependencyFilesService.Verify(x => x.DeleteExtensionsFromDependencies(It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task Update_ShouldDeleteUnselectedItemExtensionsUnlessOwnedElsewhere()
    {
        SetupWorkshopMod(pbos: ["core.pbo"], extensions: ["ext.dll", "old.dll"]);
        AddOtherMod(extensions: ["old.dll"]);

        await Update().ExecuteAsync("test-mod-123", ["core.pbo"], ["ext.dll"]);

        _deletedExtensions.Should().BeEquivalentTo("helper.dll");
    }

    [Fact]
    public async Task Install_ShouldDeleteUnselectedItemFilesAfterCopying()
    {
        var workshopMod = SetupWorkshopMod();
        var sequence = new MockSequence();
        _mockDependencyFilesService.InSequence(sequence)
                                   .Setup(x => x.CopyPbosToDependencies(workshopMod, It.IsAny<List<string>>(), It.IsAny<CancellationToken>()));
        _mockDependencyFilesService.InSequence(sequence)
                                   .Setup(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>()))
                                   .Callback<List<string>>(x => _deletedPbos = x);

        var result = await Install().ExecuteAsync("test-mod-123", ["core.pbo", "compat_a.pbo"], ["ext.dll"]);

        result.Success.Should().BeTrue();
        _deletedPbos.Should().BeEquivalentTo("compat_b.pbo");
        _deletedExtensions.Should().BeEquivalentTo("helper.dll");
        workshopMod.Pbos.Should().BeEquivalentTo("core.pbo", "compat_a.pbo");
    }

    [Fact]
    public async Task Install_WhenEverythingSelected_ShouldNotDelete()
    {
        SetupWorkshopMod();

        await Install().ExecuteAsync("test-mod-123", ["core.pbo", "compat_a.pbo", "compat_b.pbo"], ["ext.dll", "helper.dll"]);

        _mockDependencyFilesService.Verify(x => x.DeletePbosFromDependencies(It.IsAny<List<string>>()), Times.Never);
        _mockDependencyFilesService.Verify(x => x.DeleteExtensionsFromDependencies(It.IsAny<List<string>>()), Times.Never);
    }
}
