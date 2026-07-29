using FluentAssertions;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Context;
using UKSF.Api.Modpack.Services;
using Xunit;

namespace UKSF.Api.Modpack.Tests.Services;

public class WorkshopModsProcessingServiceDownloadTests
{
    private readonly Mock<IVariablesService> _variablesService = new();
    private readonly Mock<ISteamCmdService> _steamCmdService = new();
    private readonly Mock<IFileSystemService> _fileSystemService = new();
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly WorkshopModsProcessingService _subject;
    private readonly string _workshopPath = Path.Combine("C:\\steam", "steamapps", "workshop", "content", "107410", "123");

    public WorkshopModsProcessingServiceDownloadTests()
    {
        _variablesService.Setup(x => x.GetVariable("SERVER_PATH_STEAM")).Returns(new DomainVariableItem { Key = "SERVER_PATH_STEAM", Item = "C:\\steam" });
        _subject = new WorkshopModsProcessingService(
            new Mock<IWorkshopModsContext>().Object,
            _variablesService.Object,
            _steamCmdService.Object,
            new Mock<IModpackService>().Object,
            _fileSystemService.Object,
            _logger.Object
        );
    }

    private void SetupDownloadedFiles()
    {
        _fileSystemService.Setup(x => x.DirectoryExists(_workshopPath)).Returns(true);
        _fileSystemService.Setup(x => x.EnumerateFiles(_workshopPath, "*", SearchOption.AllDirectories)).Returns([Path.Combine(_workshopPath, "mod.pbo")]);
    }

    [Fact]
    public async Task DownloadWithRetries_WhenSuccessful_ShouldReturn()
    {
        _steamCmdService.Setup(x => x.DownloadWorkshopMod("123")).ReturnsAsync("ok");
        SetupDownloadedFiles();

        await _subject.DownloadWithRetries("123", 1);

        _steamCmdService.Verify(x => x.DownloadWorkshopMod("123"), Times.Once);
    }

    [Fact]
    public async Task DownloadWithRetries_WhenFirstRoundFails_ShouldClearCacheAndRetry()
    {
        SetupFailingFirstDownload();
        SetupDownloadedFiles();

        await _subject.DownloadWithRetries("123", 1);

        _steamCmdService.Verify(x => x.DownloadWorkshopMod("123"), Times.Exactly(2));
        _fileSystemService.Verify(x => x.DeleteFile(It.IsAny<string>()), Times.AtMostOnce);
        _fileSystemService.Verify(x => x.DeleteDirectory(It.IsAny<string>(), true), Times.AtMostOnce);
    }

    [Fact]
    public async Task DownloadWithRetries_WhenFirstRoundFails_ShouldDeleteModContentFolderBeforeRetrying()
    {
        SetupFailingFirstDownload();
        SetupDownloadedFiles();

        await _subject.DownloadWithRetries("123", 1);

        _fileSystemService.Verify(x => x.DeleteDirectory(_workshopPath, true), Times.Once);
    }

    [Fact]
    public async Task DownloadWithRetries_WhenBothRoundsFail_ShouldThrow()
    {
        _steamCmdService.Setup(x => x.DownloadWorkshopMod("123")).ThrowsAsync(new Exception("download failed"));

        var action = async () => await _subject.DownloadWithRetries("123", 1);

        await action.Should().ThrowAsync<Exception>().WithMessage("*clearing cache*");
        _steamCmdService.Verify(x => x.DownloadWorkshopMod("123"), Times.Exactly(2));
    }

    [Fact]
    public async Task DownloadWithRetries_WhenSteamCmdSucceedsButNoFolderDownloaded_ShouldThrow()
    {
        _steamCmdService.Setup(x => x.DownloadWorkshopMod("123")).ReturnsAsync("Success. Downloaded item 123");
        _fileSystemService.Setup(x => x.DirectoryExists(_workshopPath)).Returns(false);

        var action = async () => await _subject.DownloadWithRetries("123", 1);

        await action.Should().ThrowAsync<Exception>().WithMessage("*no files*");
    }

    [Fact]
    public async Task DownloadWithRetries_WhenSteamCmdSucceedsButFolderEmpty_ShouldThrow()
    {
        _steamCmdService.Setup(x => x.DownloadWorkshopMod("123")).ReturnsAsync("Success. Downloaded item 123");
        _fileSystemService.Setup(x => x.DirectoryExists(_workshopPath)).Returns(true);
        _fileSystemService.Setup(x => x.EnumerateFiles(_workshopPath, "*", SearchOption.AllDirectories)).Returns([]);

        var action = async () => await _subject.DownloadWithRetries("123", 1);

        await action.Should().ThrowAsync<Exception>().WithMessage("*no files*");
    }

    private void SetupFailingFirstDownload()
    {
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
    }
}
