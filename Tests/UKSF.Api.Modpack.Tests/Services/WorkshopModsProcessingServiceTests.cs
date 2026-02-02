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
}
