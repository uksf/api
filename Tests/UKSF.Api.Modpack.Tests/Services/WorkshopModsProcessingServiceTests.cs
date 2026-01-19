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
    public void GetModFiles_WhenNoPbos_ShouldThrow()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            Action action = () => _subject.GetModFiles(tempDir);

            action.Should().Throw<InvalidOperationException>().WithMessage("*No PBO files*");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GetModFiles_WhenPbosExist_ShouldReturnDistinctNames()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "nested"));
            File.WriteAllText(Path.Combine(tempDir, "mod1.pbo"), "data");
            File.WriteAllText(Path.Combine(tempDir, "nested", "mod2.pbo"), "data");

            var result = _subject.GetModFiles(tempDir);

            result.Should().BeEquivalentTo("mod1.pbo", "mod2.pbo");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
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
    public async Task CopyPbosToDependencies_ShouldCopyToDevAndRc()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var steamPath = Path.Combine(tempRoot, "steam");
            var devPath = Path.Combine(tempRoot, "dev");
            var rcPath = Path.Combine(tempRoot, "rc");
            ConfigurePaths(steamPath, devPath, rcPath);

            var workshopModId = "123";
            var workshopModPath = Path.Combine(steamPath, "steamapps", "workshop", "content", "107410", workshopModId);
            Directory.CreateDirectory(workshopModPath);

            File.WriteAllText(Path.Combine(workshopModPath, "mod1.pbo"), "data");
            File.WriteAllText(Path.Combine(workshopModPath, "mod2.pbo"), "data");

            Directory.CreateDirectory(Path.Combine(devPath, "Repo", "@uksf_dependencies", "addons"));
            Directory.CreateDirectory(Path.Combine(rcPath, "Repo", "@uksf_dependencies", "addons"));

            var workshopMod = new DomainWorkshopMod { SteamId = workshopModId, Id = "mod-id" };

            await _subject.CopyPbosToDependencies(workshopMod, ["mod1.pbo", "mod2.pbo"]);

            File.Exists(Path.Combine(devPath, "Repo", "@uksf_dependencies", "addons", "mod1.pbo")).Should().BeTrue();
            File.Exists(Path.Combine(devPath, "Repo", "@uksf_dependencies", "addons", "mod2.pbo")).Should().BeTrue();
            File.Exists(Path.Combine(rcPath, "Repo", "@uksf_dependencies", "addons", "mod1.pbo")).Should().BeTrue();
            File.Exists(Path.Combine(rcPath, "Repo", "@uksf_dependencies", "addons", "mod2.pbo")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void DeletePbosFromDependencies_ShouldDeleteFiles()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var devPath = Path.Combine(tempRoot, "dev");
            var rcPath = Path.Combine(tempRoot, "rc");
            ConfigurePaths("unused", devPath, rcPath);

            var devAddons = Path.Combine(devPath, "Repo", "@uksf_dependencies", "addons");
            var rcAddons = Path.Combine(rcPath, "Repo", "@uksf_dependencies", "addons");
            Directory.CreateDirectory(devAddons);
            Directory.CreateDirectory(rcAddons);

            File.WriteAllText(Path.Combine(devAddons, "mod1.pbo"), "data");
            File.WriteAllText(Path.Combine(rcAddons, "mod1.pbo"), "data");

            _subject.DeletePbosFromDependencies(["mod1.pbo"]);

            File.Exists(Path.Combine(devAddons, "mod1.pbo")).Should().BeFalse();
            File.Exists(Path.Combine(rcAddons, "mod1.pbo")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void CleanupWorkshopModFiles_ShouldDeleteDirectory()
    {
        var tempDir = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(tempDir, "nested"));

        _subject.CleanupWorkshopModFiles(tempDir);

        Directory.Exists(tempDir).Should().BeFalse();
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

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"uksf-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private void ConfigurePaths(string steamPath, string devPath, string rcPath)
    {
        _variablesService.Setup(x => x.GetVariable("SERVER_PATH_STEAM")).Returns(new DomainVariableItem { Key = "SERVER_PATH_STEAM", Item = steamPath });
        _variablesService.Setup(x => x.GetVariable("MODPACK_PATH_DEV")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_DEV", Item = devPath });
        _variablesService.Setup(x => x.GetVariable("MODPACK_PATH_RC")).Returns(new DomainVariableItem { Key = "MODPACK_PATH_RC", Item = rcPath });
    }
}
