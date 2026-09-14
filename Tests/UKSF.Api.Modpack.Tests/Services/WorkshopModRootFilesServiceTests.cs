using FluentAssertions;
using Moq;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using Xunit;

namespace UKSF.Api.Modpack.Tests.Services;

public class WorkshopModRootFilesServiceTests
{
    private readonly Mock<IVariablesService> _variablesService = new();
    private readonly Mock<IFileSystemService> _fileSystemService = new();
    private readonly WorkshopModRootFilesService _subject;

    private readonly string _workshopPath = Path.Combine("C:\\steam", "steamapps", "workshop", "content", "107410", "123");
    private readonly string _devPath = Path.Combine("C:\\dev", "Repo", "@TestMod");
    private readonly string _rcPath = Path.Combine("C:\\rc", "Repo", "@TestMod");

    public WorkshopModRootFilesServiceTests()
    {
        _variablesService.Setup(x => x.GetVariable("SERVER_PATH_STEAM")).Returns(new DomainVariableItem { Key = "SERVER_PATH_STEAM", Item = "C:\\steam" });
        _variablesService.Setup(x => x.GetVariable("MODPACK_PATH_DEV")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_DEV", Item = "C:\\dev" });
        _variablesService.Setup(x => x.GetVariable("MODPACK_PATH_RC")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_RC", Item = "C:\\rc" });
        _subject = new WorkshopModRootFilesService(_variablesService.Object, _fileSystemService.Object);
    }

    private static DomainWorkshopMod CreateWorkshopMod(string name = "Test Mod", string folderName = "@TestMod")
    {
        return new DomainWorkshopMod
        {
            Name = name,
            FolderName = folderName,
            SteamId = "123",
            RootMod = true
        };
    }

    [Theory]
    [InlineData("@CustomFolder", "@CustomFolder")]
    [InlineData(null, "@CBA A3")]
    [InlineData("", "@CBA A3")]
    public void GetRootModFolderName_ShouldPreferFolderNameThenDeriveFromName(string folderName, string expected)
    {
        var result = _subject.GetRootModFolderName(CreateWorkshopMod("CBA A3", folderName));

        result.Should().Be(expected);
    }

    [Fact]
    public void DeleteRootModFromRepos_DeletesFromBothRepos()
    {
        _fileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);

        _subject.DeleteRootModFromRepos(CreateWorkshopMod());

        _fileSystemService.Verify(x => x.DeleteDirectory(_devPath, true), Times.Once);
        _fileSystemService.Verify(x => x.DeleteDirectory(_rcPath, true), Times.Once);
    }

    [Fact]
    public void DeleteRootModFromRepos_WhenDirectoriesDoNotExist_DoesNotDelete()
    {
        _fileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(false);

        _subject.DeleteRootModFromRepos(CreateWorkshopMod());

        _fileSystemService.Verify(x => x.DeleteDirectory(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void DeleteRootModFromRepos_WithoutFolderName_UsesDerivedName()
    {
        _fileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);

        _subject.DeleteRootModFromRepos(CreateWorkshopMod("CBA A3", null));

        _fileSystemService.Verify(x => x.DeleteDirectory(Path.Combine("C:\\dev", "Repo", "@CBA A3"), true), Times.Once);
        _fileSystemService.Verify(x => x.DeleteDirectory(Path.Combine("C:\\rc", "Repo", "@CBA A3"), true), Times.Once);
    }

    [Fact]
    public void SyncRootModToRepos_WhenFilesAreIdentical_ShouldNotCopy()
    {
        var sourceFile = Path.Combine(_workshopPath, "addons", "mod.pbo");
        var devFile = Path.Combine(_devPath, "addons", "mod.pbo");
        var rcFile = Path.Combine(_rcPath, "addons", "mod.pbo");
        _fileSystemService.Setup(x => x.EnumerateFiles(_workshopPath, "*", SearchOption.AllDirectories)).Returns([sourceFile]);
        _fileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        _fileSystemService.Setup(x => x.EnumerateFiles(_devPath, "*", SearchOption.AllDirectories)).Returns([devFile]);
        _fileSystemService.Setup(x => x.EnumerateFiles(_rcPath, "*", SearchOption.AllDirectories)).Returns([rcFile]);
        _fileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        _fileSystemService.Setup(x => x.AreFilesEqual(sourceFile, devFile)).Returns(true);
        _fileSystemService.Setup(x => x.AreFilesEqual(sourceFile, rcFile)).Returns(true);

        var result = _subject.SyncRootModToRepos(CreateWorkshopMod());

        result.Should().BeFalse();
        _fileSystemService.Verify(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        _fileSystemService.Verify(x => x.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void SyncRootModToRepos_WhenDestDoesNotExist_ShouldCreateAndCopyAll()
    {
        var sourceFile = Path.Combine(_workshopPath, "extension_x64.dll");
        _fileSystemService.Setup(x => x.EnumerateFiles(_workshopPath, "*", SearchOption.AllDirectories)).Returns([sourceFile]);
        _fileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(false);

        var result = _subject.SyncRootModToRepos(CreateWorkshopMod());

        result.Should().BeTrue();
        _fileSystemService.Verify(x => x.CreateDirectory(_devPath), Times.Once);
        _fileSystemService.Verify(x => x.CreateDirectory(_rcPath), Times.Once);
        _fileSystemService.Verify(x => x.CopyFile(sourceFile, Path.Combine(_devPath, "extension_x64.dll"), true), Times.Once);
        _fileSystemService.Verify(x => x.CopyFile(sourceFile, Path.Combine(_rcPath, "extension_x64.dll"), true), Times.Once);
    }

    [Fact]
    public void SyncRootModToRepos_WhenEmptyDirectoriesRemain_ShouldCleanThem()
    {
        var oldDevFile = Path.Combine(_devPath, "addons", "old.pbo");
        var oldRcFile = Path.Combine(_rcPath, "addons", "old.pbo");
        _fileSystemService.Setup(x => x.EnumerateFiles(_workshopPath, "*", SearchOption.AllDirectories)).Returns([]);
        _fileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
        _fileSystemService.Setup(x => x.EnumerateFiles(_devPath, "*", SearchOption.AllDirectories)).Returns([oldDevFile]);
        _fileSystemService.Setup(x => x.EnumerateFiles(_rcPath, "*", SearchOption.AllDirectories)).Returns([oldRcFile]);

        var emptyDevDir = Path.Combine(_devPath, "addons");
        var emptyRcDir = Path.Combine(_rcPath, "addons");
        _fileSystemService.Setup(x => x.EnumerateFiles(emptyDevDir, "*", SearchOption.AllDirectories)).Returns([]);
        _fileSystemService.Setup(x => x.EnumerateFiles(emptyRcDir, "*", SearchOption.AllDirectories)).Returns([]);

        var result = _subject.SyncRootModToRepos(CreateWorkshopMod());

        result.Should().BeTrue();
        _fileSystemService.Verify(x => x.DeleteFile(oldDevFile), Times.Once);
        _fileSystemService.Verify(x => x.DeleteFile(oldRcFile), Times.Once);
        _fileSystemService.Verify(x => x.DeleteDirectory(emptyDevDir, true), Times.Once);
        _fileSystemService.Verify(x => x.DeleteDirectory(emptyRcDir, true), Times.Once);
    }

    [Fact]
    public void SyncRootModToRepos_MixedChanges_ShouldHandleAllCorrectly()
    {
        var unchangedSource = Path.Combine(_workshopPath, "addons", "unchanged.pbo");
        var changedSource = Path.Combine(_workshopPath, "addons", "changed.pbo");
        var newSource = Path.Combine(_workshopPath, "addons", "new.pbo");
        _fileSystemService.Setup(x => x.EnumerateFiles(_workshopPath, "*", SearchOption.AllDirectories)).Returns([unchangedSource, changedSource, newSource]);
        _fileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);

        var unchangedDev = Path.Combine(_devPath, "addons", "unchanged.pbo");
        var changedDev = Path.Combine(_devPath, "addons", "changed.pbo");
        var removedDev = Path.Combine(_devPath, "addons", "removed.pbo");
        var unchangedRc = Path.Combine(_rcPath, "addons", "unchanged.pbo");
        var changedRc = Path.Combine(_rcPath, "addons", "changed.pbo");
        var removedRc = Path.Combine(_rcPath, "addons", "removed.pbo");
        _fileSystemService.Setup(x => x.EnumerateFiles(_devPath, "*", SearchOption.AllDirectories)).Returns([unchangedDev, changedDev, removedDev]);
        _fileSystemService.Setup(x => x.EnumerateFiles(_rcPath, "*", SearchOption.AllDirectories)).Returns([unchangedRc, changedRc, removedRc]);
        _fileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        _fileSystemService.Setup(x => x.AreFilesEqual(unchangedSource, unchangedDev)).Returns(true);
        _fileSystemService.Setup(x => x.AreFilesEqual(unchangedSource, unchangedRc)).Returns(true);
        _fileSystemService.Setup(x => x.AreFilesEqual(changedSource, changedDev)).Returns(false);
        _fileSystemService.Setup(x => x.AreFilesEqual(changedSource, changedRc)).Returns(false);

        var result = _subject.SyncRootModToRepos(CreateWorkshopMod());

        result.Should().BeTrue();
        _fileSystemService.Verify(x => x.CopyFile(unchangedSource, unchangedDev, true), Times.Never);
        _fileSystemService.Verify(x => x.CopyFile(unchangedSource, unchangedRc, true), Times.Never);
        _fileSystemService.Verify(x => x.CopyFile(changedSource, changedDev, true), Times.Once);
        _fileSystemService.Verify(x => x.CopyFile(changedSource, changedRc, true), Times.Once);
        _fileSystemService.Verify(x => x.CopyFile(newSource, Path.Combine(_devPath, "addons", "new.pbo"), true), Times.Once);
        _fileSystemService.Verify(x => x.CopyFile(newSource, Path.Combine(_rcPath, "addons", "new.pbo"), true), Times.Once);
        _fileSystemService.Verify(x => x.DeleteFile(removedDev), Times.Once);
        _fileSystemService.Verify(x => x.DeleteFile(removedRc), Times.Once);
    }
}
