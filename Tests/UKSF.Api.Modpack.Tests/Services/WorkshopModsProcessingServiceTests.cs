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
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly WorkshopModsProcessingService _subject;

    public WorkshopModsProcessingServiceTests()
    {
        _subject = new WorkshopModsProcessingService(
            _context.Object,
            _variablesService.Object,
            _steamCmdService.Object,
            _modpackService.Object,
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
    public async Task DownloadWithRetries_WhenFailureOnLastAttempt_ShouldThrow()
    {
        _steamCmdService.Setup(x => x.DownloadWorkshopMod("123")).ThrowsAsync(new Exception("download failed"));

        var action = async () => await _subject.DownloadWithRetries("123", 1);

        await action.Should().ThrowAsync<Exception>().WithMessage("*download failed*");
    }

    [Fact]
    public async Task QueueDevBuild_ShouldCancelRunningBuildsAndCreateNew()
    {
        var build1 = new DomainModpackBuild { Id = "build1", Running = true };
        var build2 = new DomainModpackBuild { Id = "build2", Running = true };
        _modpackService.Setup(x => x.GetDevBuilds()).Returns([build1, build2]);
        _modpackService.Setup(x => x.CancelBuild(build1)).Returns(Task.CompletedTask);
        _modpackService.Setup(x => x.CancelBuild(build2)).Returns(Task.CompletedTask);
        _modpackService.Setup(x => x.NewBuild(It.IsAny<NewBuild>())).Returns(Task.CompletedTask);

        await _subject.QueueDevBuild();

        _modpackService.Verify(x => x.CancelBuild(build1), Times.Once);
        _modpackService.Verify(x => x.CancelBuild(build2), Times.Once);
        _modpackService.Verify(x => x.NewBuild(It.Is<NewBuild>(b => b.Reference == "main")), Times.Once);
    }

    [Fact]
    public async Task QueueDevBuild_WhenExceptionOccurs_ShouldLogError()
    {
        _modpackService.Setup(x => x.GetDevBuilds()).Throws(new InvalidOperationException("fail"));

        await _subject.QueueDevBuild();

        _logger.Verify(x => x.LogError(It.Is<string>(s => s.Contains("Failed to trigger dev build")), It.IsAny<Exception>()), Times.Once);
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
}
