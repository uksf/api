using FluentAssertions;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using Xunit;

namespace UKSF.Api.Modpack.Tests.Services;

public class WorkshopModsProcessingServiceTests
{
    private readonly Mock<IWorkshopModsContext> _context = new();
    private readonly Mock<IVariablesService> _variablesService = new();
    private readonly Mock<ISteamCmdService> _steamCmdService = new();
    private readonly Mock<IModpackService> _modpackService = new();
    private readonly Mock<IFileSystemService> _fileSystemService = new();
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly WorkshopModsProcessingService _subject;

    public WorkshopModsProcessingServiceTests()
    {
        _subject = new WorkshopModsProcessingService(
            _context.Object,
            _variablesService.Object,
            _steamCmdService.Object,
            _modpackService.Object,
            _fileSystemService.Object,
            _logger.Object
        );
    }

    [Fact]
    public void GetWorkshopModPath_ShouldCombineSteamPath()
    {
        _variablesService.Setup(x => x.GetVariable("SERVER_PATH_STEAM")).Returns(new DomainVariableItem { Key = "SERVER_PATH_STEAM", Item = "C:\\steam" });

        var result = _subject.GetWorkshopModPath("123");

        result.Should().Be(Path.Combine("C:\\steam", "steamapps", "workshop", "content", "107410", "123"));
    }

    [Fact]
    public async Task DownloadWithRetries_WhenSuccessful_ShouldReturn()
    {
        _steamCmdService.Setup(x => x.DownloadWorkshopMod("123")).ReturnsAsync("ok");

        await _subject.DownloadWithRetries("123", 1);

        _steamCmdService.Verify(x => x.DownloadWorkshopMod("123"), Times.Once);
    }

    [Fact]
    public async Task DownloadWithRetries_WhenFirstRoundFails_ShouldClearCacheAndRetry()
    {
        _variablesService.Setup(x => x.GetVariable("SERVER_PATH_STEAM")).Returns(new DomainVariableItem { Key = "SERVER_PATH_STEAM", Item = "C:\\steam" });
        var callCount = 0;
        _steamCmdService.Setup(x => x.DownloadWorkshopMod("123"))
                        .Returns(() =>
                            {
                                callCount++;
                                if (callCount <= 1)
                                {
                                    throw new Exception("download failed");
                                }

                                return Task.FromResult("ok");
                            }
                        );

        await _subject.DownloadWithRetries("123", 1);

        _steamCmdService.Verify(x => x.DownloadWorkshopMod("123"), Times.Exactly(2));
        _fileSystemService.Verify(x => x.DeleteFile(It.IsAny<string>()), Times.AtMostOnce);
        _fileSystemService.Verify(x => x.DeleteDirectory(It.IsAny<string>(), true), Times.AtMostOnce);
    }

    [Fact]
    public async Task DownloadWithRetries_WhenBothRoundsFail_ShouldThrow()
    {
        _variablesService.Setup(x => x.GetVariable("SERVER_PATH_STEAM")).Returns(new DomainVariableItem { Key = "SERVER_PATH_STEAM", Item = "C:\\steam" });
        _steamCmdService.Setup(x => x.DownloadWorkshopMod("123")).ThrowsAsync(new Exception("download failed"));

        var action = async () => await _subject.DownloadWithRetries("123", 1);

        await action.Should().ThrowAsync<Exception>().WithMessage("*clearing cache*");
        _steamCmdService.Verify(x => x.DownloadWorkshopMod("123"), Times.Exactly(2));
    }

    [Fact]
    public async Task QueueDevBuild_ShouldCancelRunningBuildsAndStartNew()
    {
        var runningBuild = new DomainModpackBuild { Running = true };
        _modpackService.Setup(x => x.GetDevBuilds()).Returns([runningBuild]);
        _modpackService.Setup(x => x.CancelBuild(runningBuild)).Returns(Task.CompletedTask);
        _modpackService.Setup(x => x.NewBuild(It.IsAny<NewBuild>())).Returns(Task.CompletedTask);

        await _subject.QueueDevBuild();

        _modpackService.Verify(x => x.GetDevBuilds(), Times.Once);
        _modpackService.Verify(x => x.CancelBuild(runningBuild), Times.Once);
        _modpackService.Verify(x => x.NewBuild(It.Is<NewBuild>(b => b.Reference == "main")), Times.Once);
    }

    [Fact]
    public async Task QueueDevBuild_WhenNoRunningBuilds_ShouldOnlyStartNew()
    {
        _modpackService.Setup(x => x.GetDevBuilds()).Returns([]);
        _modpackService.Setup(x => x.NewBuild(It.IsAny<NewBuild>())).Returns(Task.CompletedTask);

        await _subject.QueueDevBuild();

        _modpackService.Verify(x => x.GetDevBuilds(), Times.Once);
        _modpackService.Verify(x => x.CancelBuild(It.IsAny<DomainModpackBuild>()), Times.Never);
        _modpackService.Verify(x => x.NewBuild(It.Is<NewBuild>(b => b.Reference == "main")), Times.Once);
    }

    [Fact]
    public async Task QueueDevBuild_WhenExceptionThrown_ShouldLogErrorAndNotRethrow()
    {
        _modpackService.Setup(x => x.GetDevBuilds()).Throws(new Exception("test error"));

        await _subject.QueueDevBuild();

        _logger.Verify(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
    }

    [Fact]
    public async Task UpdateModStatus_WhenError_ShouldSetErrorMessage()
    {
        var workshopMod = new DomainWorkshopMod { Id = "mod-id" };
        _context.Setup(x => x.Replace(workshopMod)).Returns(Task.CompletedTask);

        await _subject.UpdateModStatus(workshopMod, WorkshopModStatus.Error, "failed");

        workshopMod.ErrorMessage.Should().Be("failed");
        _context.Verify(x => x.Replace(workshopMod), Times.Once);
    }

    [Fact]
    public async Task UpdateModStatus_WhenNonError_ShouldSetStatusMessage()
    {
        var workshopMod = new DomainWorkshopMod { Id = "mod-id" };
        _context.Setup(x => x.Replace(workshopMod)).Returns(Task.CompletedTask);

        await _subject.UpdateModStatus(workshopMod, WorkshopModStatus.Installing, "working");

        workshopMod.StatusMessage.Should().Be("working");
        _context.Verify(x => x.Replace(workshopMod), Times.Once);
    }

    [Fact]
    public void DeletePbosFromDependencies_WhenFilesExist_ShouldDelete()
    {
        _variablesService.Setup(x => x.GetVariable("MODPACK_PATH_DEV")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_DEV", Item = "C:\\dev" });
        _variablesService.Setup(x => x.GetVariable("MODPACK_PATH_RC")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_RC", Item = "C:\\rc" });
        _fileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);

        _subject.DeletePbosFromDependencies(new List<string> { "test.pbo" });

        var expectedDevPath = Path.Combine("C:\\dev", "Repo", "@uksf_dependencies", "addons", "test.pbo");
        var expectedRcPath = Path.Combine("C:\\rc", "Repo", "@uksf_dependencies", "addons", "test.pbo");
        _fileSystemService.Verify(x => x.DeleteFile(expectedDevPath), Times.Once);
        _fileSystemService.Verify(x => x.DeleteFile(expectedRcPath), Times.Once);
    }

    [Fact]
    public void DeletePbosFromDependencies_WhenFilesDoNotExist_ShouldNotDelete()
    {
        _variablesService.Setup(x => x.GetVariable("MODPACK_PATH_DEV")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_DEV", Item = "C:\\dev" });
        _variablesService.Setup(x => x.GetVariable("MODPACK_PATH_RC")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_RC", Item = "C:\\rc" });
        _fileSystemService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);

        _subject.DeletePbosFromDependencies(new List<string> { "test.pbo" });

        _fileSystemService.Verify(x => x.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void CleanupWorkshopModFiles_WhenDirectoryExists_ShouldDelete()
    {
        _fileSystemService.Setup(x => x.DirectoryExists("C:\\workshop\\mod")).Returns(true);

        _subject.CleanupWorkshopModFiles("C:\\workshop\\mod");

        _fileSystemService.Verify(x => x.DeleteDirectory("C:\\workshop\\mod", true), Times.Once);
    }

    [Fact]
    public void CleanupWorkshopModFiles_WhenDirectoryDoesNotExist_ShouldNotDelete()
    {
        _fileSystemService.Setup(x => x.DirectoryExists("C:\\workshop\\mod")).Returns(false);

        _subject.CleanupWorkshopModFiles("C:\\workshop\\mod");

        _fileSystemService.Verify(x => x.DeleteDirectory(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void GetRootModFolderName_WithFolderName_ReturnsFolderName()
    {
        var workshopMod = new DomainWorkshopMod { Name = "Test Mod", FolderName = "@CustomFolder" };

        var result = _subject.GetRootModFolderName(workshopMod);

        result.Should().Be("@CustomFolder");
    }

    [Fact]
    public void GetRootModFolderName_WithoutFolderName_DerivesFromName()
    {
        var workshopMod = new DomainWorkshopMod { Name = "CBA A3", FolderName = null };

        var result = _subject.GetRootModFolderName(workshopMod);

        result.Should().Be("@CBA A3");
    }

    [Fact]
    public void GetRootModFolderName_WithEmptyFolderName_DerivesFromName()
    {
        var workshopMod = new DomainWorkshopMod { Name = "CUP Terrains Core", FolderName = "" };

        var result = _subject.GetRootModFolderName(workshopMod);

        result.Should().Be("@CUP Terrains Core");
    }

    [Fact]
    public void DeleteRootModFromRepos_DeletesFromBothRepos()
    {
        var workshopMod = new DomainWorkshopMod { Name = "Test Mod", FolderName = "@TestMod" };
        _variablesService.Setup(x => x.GetVariable("MODPACK_PATH_DEV")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_DEV", Item = "C:\\dev" });
        _variablesService.Setup(x => x.GetVariable("MODPACK_PATH_RC")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_RC", Item = "C:\\rc" });
        _fileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);

        _subject.DeleteRootModFromRepos(workshopMod);

        var expectedDevPath = Path.Combine("C:\\dev", "Repo", "@TestMod");
        var expectedRcPath = Path.Combine("C:\\rc", "Repo", "@TestMod");
        _fileSystemService.Verify(x => x.DeleteDirectory(expectedDevPath, true), Times.Once);
        _fileSystemService.Verify(x => x.DeleteDirectory(expectedRcPath, true), Times.Once);
    }

    [Fact]
    public void DeleteRootModFromRepos_WhenDirectoriesDoNotExist_DoesNotDelete()
    {
        var workshopMod = new DomainWorkshopMod { Name = "Test Mod", FolderName = "@TestMod" };
        _variablesService.Setup(x => x.GetVariable("MODPACK_PATH_DEV")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_DEV", Item = "C:\\dev" });
        _variablesService.Setup(x => x.GetVariable("MODPACK_PATH_RC")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_RC", Item = "C:\\rc" });
        _fileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(false);

        _subject.DeleteRootModFromRepos(workshopMod);

        _fileSystemService.Verify(x => x.DeleteDirectory(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void DeleteRootModFromRepos_WithoutFolderName_UsesDerivedName()
    {
        var workshopMod = new DomainWorkshopMod { Name = "CBA A3", FolderName = null };
        _variablesService.Setup(x => x.GetVariable("MODPACK_PATH_DEV")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_DEV", Item = "C:\\dev" });
        _variablesService.Setup(x => x.GetVariable("MODPACK_PATH_RC")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_RC", Item = "C:\\rc" });
        _fileSystemService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);

        _subject.DeleteRootModFromRepos(workshopMod);

        var expectedDevPath = Path.Combine("C:\\dev", "Repo", "@CBA A3");
        var expectedRcPath = Path.Combine("C:\\rc", "Repo", "@CBA A3");
        _fileSystemService.Verify(x => x.DeleteDirectory(expectedDevPath, true), Times.Once);
        _fileSystemService.Verify(x => x.DeleteDirectory(expectedRcPath, true), Times.Once);
    }

    // SyncRootModToRepos tests

    private DomainWorkshopMod CreateWorkshopMod(string name = "Test Mod", string folderName = "@TestMod", string steamId = "123")
    {
        return new DomainWorkshopMod
        {
            Name = name,
            FolderName = folderName,
            SteamId = steamId,
            RootMod = true
        };
    }

    private void SetupRepoPaths()
    {
        _variablesService.Setup(x => x.GetVariable("SERVER_PATH_STEAM")).Returns(new DomainVariableItem { Key = "SERVER_PATH_STEAM", Item = "C:\\steam" });
        _variablesService.Setup(x => x.GetVariable("MODPACK_PATH_DEV")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_DEV", Item = "C:\\dev" });
        _variablesService.Setup(x => x.GetVariable("MODPACK_PATH_RC")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_RC", Item = "C:\\rc" });
    }

    [Fact]
    public void SyncRootModToRepos_WhenNewFilesExist_ShouldCopyThem()
    {
        var workshopMod = CreateWorkshopMod();
        SetupRepoPaths();
        var workshopPath = Path.Combine("C:\\steam", "steamapps", "workshop", "content", "107410", "123");
        var devPath = Path.Combine("C:\\dev", "Repo", "@TestMod");
        var rcPath = Path.Combine("C:\\rc", "Repo", "@TestMod");

        var sourceFile = Path.Combine(workshopPath, "addons", "mod.pbo");
        _fileSystemService.Setup(x => x.EnumerateFiles(workshopPath, "*", SearchOption.AllDirectories)).Returns([sourceFile]);
        _fileSystemService.Setup(x => x.DirectoryExists(devPath)).Returns(true);
        _fileSystemService.Setup(x => x.DirectoryExists(rcPath)).Returns(true);
        _fileSystemService.Setup(x => x.EnumerateFiles(devPath, "*", SearchOption.AllDirectories)).Returns([]);
        _fileSystemService.Setup(x => x.EnumerateFiles(rcPath, "*", SearchOption.AllDirectories)).Returns([]);

        var result = _subject.SyncRootModToRepos(workshopMod);

        result.Should().BeTrue();
        var expectedDevFile = Path.Combine(devPath, "addons", "mod.pbo");
        var expectedRcFile = Path.Combine(rcPath, "addons", "mod.pbo");
        _fileSystemService.Verify(x => x.CreateDirectory(Path.Combine(devPath, "addons")), Times.Once);
        _fileSystemService.Verify(x => x.CreateDirectory(Path.Combine(rcPath, "addons")), Times.Once);
        _fileSystemService.Verify(x => x.CopyFile(sourceFile, expectedDevFile, true), Times.Once);
        _fileSystemService.Verify(x => x.CopyFile(sourceFile, expectedRcFile, true), Times.Once);
    }

    [Fact]
    public void SyncRootModToRepos_WhenFilesAreIdentical_ShouldNotCopy()
    {
        var workshopMod = CreateWorkshopMod();
        SetupRepoPaths();
        var workshopPath = Path.Combine("C:\\steam", "steamapps", "workshop", "content", "107410", "123");
        var devPath = Path.Combine("C:\\dev", "Repo", "@TestMod");
        var rcPath = Path.Combine("C:\\rc", "Repo", "@TestMod");

        var sourceFile = Path.Combine(workshopPath, "addons", "mod.pbo");
        var devFile = Path.Combine(devPath, "addons", "mod.pbo");
        var rcFile = Path.Combine(rcPath, "addons", "mod.pbo");
        _fileSystemService.Setup(x => x.EnumerateFiles(workshopPath, "*", SearchOption.AllDirectories)).Returns([sourceFile]);
        _fileSystemService.Setup(x => x.DirectoryExists(devPath)).Returns(true);
        _fileSystemService.Setup(x => x.DirectoryExists(rcPath)).Returns(true);
        _fileSystemService.Setup(x => x.EnumerateFiles(devPath, "*", SearchOption.AllDirectories)).Returns([devFile]);
        _fileSystemService.Setup(x => x.EnumerateFiles(rcPath, "*", SearchOption.AllDirectories)).Returns([rcFile]);
        _fileSystemService.Setup(x => x.FileExists(devFile)).Returns(true);
        _fileSystemService.Setup(x => x.FileExists(rcFile)).Returns(true);
        _fileSystemService.Setup(x => x.AreFilesEqual(sourceFile, devFile)).Returns(true);
        _fileSystemService.Setup(x => x.AreFilesEqual(sourceFile, rcFile)).Returns(true);

        var result = _subject.SyncRootModToRepos(workshopMod);

        result.Should().BeFalse();
        _fileSystemService.Verify(x => x.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        _fileSystemService.Verify(x => x.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void SyncRootModToRepos_WhenFileChanged_ShouldOverwrite()
    {
        var workshopMod = CreateWorkshopMod();
        SetupRepoPaths();
        var workshopPath = Path.Combine("C:\\steam", "steamapps", "workshop", "content", "107410", "123");
        var devPath = Path.Combine("C:\\dev", "Repo", "@TestMod");
        var rcPath = Path.Combine("C:\\rc", "Repo", "@TestMod");

        var sourceFile = Path.Combine(workshopPath, "addons", "mod.pbo");
        var devFile = Path.Combine(devPath, "addons", "mod.pbo");
        var rcFile = Path.Combine(rcPath, "addons", "mod.pbo");
        _fileSystemService.Setup(x => x.EnumerateFiles(workshopPath, "*", SearchOption.AllDirectories)).Returns([sourceFile]);
        _fileSystemService.Setup(x => x.DirectoryExists(devPath)).Returns(true);
        _fileSystemService.Setup(x => x.DirectoryExists(rcPath)).Returns(true);
        _fileSystemService.Setup(x => x.EnumerateFiles(devPath, "*", SearchOption.AllDirectories)).Returns([devFile]);
        _fileSystemService.Setup(x => x.EnumerateFiles(rcPath, "*", SearchOption.AllDirectories)).Returns([rcFile]);
        _fileSystemService.Setup(x => x.FileExists(devFile)).Returns(true);
        _fileSystemService.Setup(x => x.FileExists(rcFile)).Returns(true);
        _fileSystemService.Setup(x => x.AreFilesEqual(sourceFile, devFile)).Returns(false);
        _fileSystemService.Setup(x => x.AreFilesEqual(sourceFile, rcFile)).Returns(false);

        var result = _subject.SyncRootModToRepos(workshopMod);

        result.Should().BeTrue();
        _fileSystemService.Verify(x => x.CopyFile(sourceFile, devFile, true), Times.Once);
        _fileSystemService.Verify(x => x.CopyFile(sourceFile, rcFile, true), Times.Once);
    }

    [Fact]
    public void SyncRootModToRepos_WhenOldFilesRemoved_ShouldDeleteThem()
    {
        var workshopMod = CreateWorkshopMod();
        SetupRepoPaths();
        var workshopPath = Path.Combine("C:\\steam", "steamapps", "workshop", "content", "107410", "123");
        var devPath = Path.Combine("C:\\dev", "Repo", "@TestMod");
        var rcPath = Path.Combine("C:\\rc", "Repo", "@TestMod");

        _fileSystemService.Setup(x => x.EnumerateFiles(workshopPath, "*", SearchOption.AllDirectories)).Returns([]);
        _fileSystemService.Setup(x => x.DirectoryExists(devPath)).Returns(true);
        _fileSystemService.Setup(x => x.DirectoryExists(rcPath)).Returns(true);
        var oldDevFile = Path.Combine(devPath, "addons", "old.pbo");
        var oldRcFile = Path.Combine(rcPath, "addons", "old.pbo");
        _fileSystemService.Setup(x => x.EnumerateFiles(devPath, "*", SearchOption.AllDirectories)).Returns([oldDevFile]);
        _fileSystemService.Setup(x => x.EnumerateFiles(rcPath, "*", SearchOption.AllDirectories)).Returns([oldRcFile]);

        var result = _subject.SyncRootModToRepos(workshopMod);

        result.Should().BeTrue();
        _fileSystemService.Verify(x => x.DeleteFile(oldDevFile), Times.Once);
        _fileSystemService.Verify(x => x.DeleteFile(oldRcFile), Times.Once);
    }

    [Fact]
    public void SyncRootModToRepos_WhenDestDoesNotExist_ShouldCreateAndCopyAll()
    {
        var workshopMod = CreateWorkshopMod();
        SetupRepoPaths();
        var workshopPath = Path.Combine("C:\\steam", "steamapps", "workshop", "content", "107410", "123");
        var devPath = Path.Combine("C:\\dev", "Repo", "@TestMod");
        var rcPath = Path.Combine("C:\\rc", "Repo", "@TestMod");

        var sourceFile = Path.Combine(workshopPath, "mod.cpp");
        _fileSystemService.Setup(x => x.EnumerateFiles(workshopPath, "*", SearchOption.AllDirectories)).Returns([sourceFile]);
        _fileSystemService.Setup(x => x.DirectoryExists(devPath)).Returns(false);
        _fileSystemService.Setup(x => x.DirectoryExists(rcPath)).Returns(false);

        var result = _subject.SyncRootModToRepos(workshopMod);

        result.Should().BeTrue();
        _fileSystemService.Verify(x => x.CreateDirectory(devPath), Times.Once);
        _fileSystemService.Verify(x => x.CreateDirectory(rcPath), Times.Once);
        _fileSystemService.Verify(x => x.CopyFile(sourceFile, Path.Combine(devPath, "mod.cpp"), true), Times.Once);
        _fileSystemService.Verify(x => x.CopyFile(sourceFile, Path.Combine(rcPath, "mod.cpp"), true), Times.Once);
    }

    [Fact]
    public void SyncRootModToRepos_WhenEmptyDirectoriesRemain_ShouldCleanThem()
    {
        var workshopMod = CreateWorkshopMod();
        SetupRepoPaths();
        var workshopPath = Path.Combine("C:\\steam", "steamapps", "workshop", "content", "107410", "123");
        var devPath = Path.Combine("C:\\dev", "Repo", "@TestMod");
        var rcPath = Path.Combine("C:\\rc", "Repo", "@TestMod");

        _fileSystemService.Setup(x => x.EnumerateFiles(workshopPath, "*", SearchOption.AllDirectories)).Returns([]);
        _fileSystemService.Setup(x => x.DirectoryExists(devPath)).Returns(true);
        _fileSystemService.Setup(x => x.DirectoryExists(rcPath)).Returns(true);
        var oldDevFile = Path.Combine(devPath, "addons", "old.pbo");
        var oldRcFile = Path.Combine(rcPath, "addons", "old.pbo");
        _fileSystemService.Setup(x => x.EnumerateFiles(devPath, "*", SearchOption.AllDirectories)).Returns([oldDevFile]);
        _fileSystemService.Setup(x => x.EnumerateFiles(rcPath, "*", SearchOption.AllDirectories)).Returns([oldRcFile]);

        var emptyDevDir = Path.Combine(devPath, "addons");
        var emptyRcDir = Path.Combine(rcPath, "addons");
        _fileSystemService.Setup(x => x.DirectoryExists(emptyDevDir)).Returns(true);
        _fileSystemService.Setup(x => x.DirectoryExists(emptyRcDir)).Returns(true);
        _fileSystemService.Setup(x => x.EnumerateFiles(emptyDevDir, "*", SearchOption.AllDirectories)).Returns([]);
        _fileSystemService.Setup(x => x.EnumerateFiles(emptyRcDir, "*", SearchOption.AllDirectories)).Returns([]);

        _subject.SyncRootModToRepos(workshopMod);

        _fileSystemService.Verify(x => x.DeleteDirectory(emptyDevDir, true), Times.Once);
        _fileSystemService.Verify(x => x.DeleteDirectory(emptyRcDir, true), Times.Once);
    }

    [Fact]
    public void SyncRootModToRepos_MixedChanges_ShouldHandleAllCorrectly()
    {
        var workshopMod = CreateWorkshopMod();
        SetupRepoPaths();
        var workshopPath = Path.Combine("C:\\steam", "steamapps", "workshop", "content", "107410", "123");
        var devPath = Path.Combine("C:\\dev", "Repo", "@TestMod");
        var rcPath = Path.Combine("C:\\rc", "Repo", "@TestMod");

        var unchangedSource = Path.Combine(workshopPath, "addons", "unchanged.pbo");
        var changedSource = Path.Combine(workshopPath, "addons", "changed.pbo");
        var newSource = Path.Combine(workshopPath, "addons", "new.pbo");
        _fileSystemService.Setup(x => x.EnumerateFiles(workshopPath, "*", SearchOption.AllDirectories)).Returns([unchangedSource, changedSource, newSource]);

        _fileSystemService.Setup(x => x.DirectoryExists(devPath)).Returns(true);
        _fileSystemService.Setup(x => x.DirectoryExists(rcPath)).Returns(true);

        var unchangedDev = Path.Combine(devPath, "addons", "unchanged.pbo");
        var changedDev = Path.Combine(devPath, "addons", "changed.pbo");
        var removedDev = Path.Combine(devPath, "addons", "removed.pbo");
        _fileSystemService.Setup(x => x.EnumerateFiles(devPath, "*", SearchOption.AllDirectories)).Returns([unchangedDev, changedDev, removedDev]);

        var unchangedRc = Path.Combine(rcPath, "addons", "unchanged.pbo");
        var changedRc = Path.Combine(rcPath, "addons", "changed.pbo");
        var removedRc = Path.Combine(rcPath, "addons", "removed.pbo");
        _fileSystemService.Setup(x => x.EnumerateFiles(rcPath, "*", SearchOption.AllDirectories)).Returns([unchangedRc, changedRc, removedRc]);

        _fileSystemService.Setup(x => x.FileExists(unchangedDev)).Returns(true);
        _fileSystemService.Setup(x => x.FileExists(changedDev)).Returns(true);
        _fileSystemService.Setup(x => x.FileExists(unchangedRc)).Returns(true);
        _fileSystemService.Setup(x => x.FileExists(changedRc)).Returns(true);

        _fileSystemService.Setup(x => x.AreFilesEqual(unchangedSource, unchangedDev)).Returns(true);
        _fileSystemService.Setup(x => x.AreFilesEqual(changedSource, changedDev)).Returns(false);
        _fileSystemService.Setup(x => x.AreFilesEqual(unchangedSource, unchangedRc)).Returns(true);
        _fileSystemService.Setup(x => x.AreFilesEqual(changedSource, changedRc)).Returns(false);

        var result = _subject.SyncRootModToRepos(workshopMod);

        result.Should().BeTrue();
        // Unchanged: not copied
        _fileSystemService.Verify(x => x.CopyFile(unchangedSource, unchangedDev, true), Times.Never);
        _fileSystemService.Verify(x => x.CopyFile(unchangedSource, unchangedRc, true), Times.Never);
        // Changed: overwritten
        _fileSystemService.Verify(x => x.CopyFile(changedSource, changedDev, true), Times.Once);
        _fileSystemService.Verify(x => x.CopyFile(changedSource, changedRc, true), Times.Once);
        // New: copied
        var newDev = Path.Combine(devPath, "addons", "new.pbo");
        var newRc = Path.Combine(rcPath, "addons", "new.pbo");
        _fileSystemService.Verify(x => x.CopyFile(newSource, newDev, true), Times.Once);
        _fileSystemService.Verify(x => x.CopyFile(newSource, newRc, true), Times.Once);
        // Removed: deleted
        _fileSystemService.Verify(x => x.DeleteFile(removedDev), Times.Once);
        _fileSystemService.Verify(x => x.DeleteFile(removedRc), Times.Once);
    }
}
