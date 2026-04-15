using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Models;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class MissionsServiceTests : IDisposable
{
    private readonly Mock<IMissionPatchingService> _mockMissionPatchingService = new();
    private readonly Mock<IGameServerHelpers> _mockGameServerHelpers = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly string _tempDir;

    private readonly MissionsService _subject;

    public MissionsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"MissionsServiceTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _subject = new MissionsService(_mockMissionPatchingService.Object, _mockGameServerHelpers.Object, _mockLogger.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task PatchMissionFile_ShouldCallPatchingServiceWithCorrectPath()
    {
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsPath()).Returns(@"C:\missions");
        _mockGameServerHelpers.Setup(x => x.GetGameServerModsPaths(GameEnvironment.Release)).Returns(@"C:\mods\Repo");
        _mockGameServerHelpers.Setup(x => x.GetMaxCuratorCountFromSettings()).Returns(5);
        _mockMissionPatchingService.Setup(x => x.PatchMission(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                                   .ReturnsAsync(new MissionPatchingResult { Success = true, PlayerCount = 40 });

        var result = await _subject.PatchMissionFile("co40_op_eagle.Altis.pbo");

        result.Success.Should().BeTrue();
        result.PlayerCount.Should().Be(40);
        _mockMissionPatchingService.Verify(x => x.PatchMission(Path.Combine(@"C:\missions", "co40_op_eagle.Altis.pbo"), @"C:\mods\Repo", 5), Times.Once);
    }

    [Fact]
    public async Task PatchMissionFile_ShouldSanitizeFilename_PreventingPathTraversal()
    {
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsPath()).Returns(@"C:\missions");
        _mockGameServerHelpers.Setup(x => x.GetGameServerModsPaths(GameEnvironment.Release)).Returns(@"C:\mods\Repo");
        _mockGameServerHelpers.Setup(x => x.GetMaxCuratorCountFromSettings()).Returns(5);
        _mockMissionPatchingService.Setup(x => x.PatchMission(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                                   .ReturnsAsync(new MissionPatchingResult { Success = true });

        await _subject.PatchMissionFile(@"..\..\etc\passwd");

        _mockMissionPatchingService.Verify(x => x.PatchMission(Path.Combine(@"C:\missions", "passwd"), It.IsAny<string>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task PatchMissionFile_ShouldReturnResultFromPatchingService()
    {
        var expectedResult = new MissionPatchingResult
        {
            Success = false,
            PlayerCount = 0,
            Reports = [new("Missing description.ext", "File not found", true)]
        };
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsPath()).Returns(@"C:\missions");
        _mockGameServerHelpers.Setup(x => x.GetGameServerModsPaths(GameEnvironment.Release)).Returns(@"C:\mods");
        _mockGameServerHelpers.Setup(x => x.GetMaxCuratorCountFromSettings()).Returns(3);
        _mockMissionPatchingService.Setup(x => x.PatchMission(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(expectedResult);

        var result = await _subject.PatchMissionFile("broken_mission.Altis.pbo");

        result.Should().BeSameAs(expectedResult);
    }

    [Fact]
    public async Task UploadMissionFile_ShouldWriteFileAndReturnSanitizedFilename()
    {
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsPath()).Returns(_tempDir);

        var content = "test file content"u8.ToArray();
        var stream = new MemoryStream(content);
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("co40_test.Altis.pbo");
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), default)).Returns<Stream, CancellationToken>((s, _) => stream.CopyToAsync(s));

        var result = await _subject.UploadMissionFile(mockFile.Object);

        result.Should().Be("co40_test.Altis.pbo");
        var filePath = Path.Combine(_tempDir, "co40_test.Altis.pbo");
        File.Exists(filePath).Should().BeTrue();
        (await File.ReadAllBytesAsync(filePath)).Should().Equal(content);
    }

    [Fact]
    public async Task UploadMissionFile_ShouldSanitizeFilenameAndReturnSafeVersion()
    {
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsPath()).Returns(_tempDir);

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(@"..\..\evil.pbo");
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), default))
                .Returns<Stream, CancellationToken>((s, _) => new MemoryStream("x"u8.ToArray()).CopyToAsync(s));

        var result = await _subject.UploadMissionFile(mockFile.Object);

        result.Should().Be("evil.pbo");
        File.Exists(Path.Combine(_tempDir, "evil.pbo")).Should().BeTrue();
        File.Exists(Path.Combine(_tempDir, "..", "..", "evil.pbo")).Should().BeFalse();
    }

    [Fact]
    public void GetActiveMissions_ShouldReturnPboFilesFromPath()
    {
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsPath()).Returns(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "co40_test.Altis.pbo"), "");
        File.WriteAllText(Path.Combine(_tempDir, "tvt20_clash.Stratis.pbo"), "");
        File.WriteAllText(Path.Combine(_tempDir, "readme.txt"), "");

        var result = _subject.GetActiveMissions();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(m => m.Path.EndsWith(".pbo"));
    }

    [Fact]
    public void GetActiveMissions_ShouldReturnEmptyWhenDirectoryDoesNotExist()
    {
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsPath()).Returns(Path.Combine(_tempDir, "nonexistent"));

        var result = _subject.GetActiveMissions();

        result.Should().BeEmpty();
    }

    [Fact]
    public void DeleteMissionFile_ShouldDeleteFileFromActivePath()
    {
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsPath()).Returns(_tempDir);
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsArchivePath()).Returns(Path.Combine(_tempDir, "archive"));
        var filePath = Path.Combine(_tempDir, "test.Altis.pbo");
        File.WriteAllText(filePath, "");

        _subject.DeleteMissionFile("test.Altis.pbo");

        File.Exists(filePath).Should().BeFalse();
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("test.Altis.pbo"))));
    }

    [Fact]
    public void DeleteMissionFile_ShouldThrowWhenNotFound()
    {
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsPath()).Returns(_tempDir);
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsArchivePath()).Returns(Path.Combine(_tempDir, "archive"));

        var act = () => _subject.DeleteMissionFile("nonexistent.pbo");

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void ArchiveMissionFile_ShouldMoveFileFromActiveToArchive()
    {
        var archiveDir = Path.Combine(_tempDir, "archive");
        Directory.CreateDirectory(archiveDir);
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsPath()).Returns(_tempDir);
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsArchivePath()).Returns(archiveDir);
        File.WriteAllText(Path.Combine(_tempDir, "test.Altis.pbo"), "content");

        _subject.ArchiveMissionFile("test.Altis.pbo");

        File.Exists(Path.Combine(_tempDir, "test.Altis.pbo")).Should().BeFalse();
        File.Exists(Path.Combine(archiveDir, "test.Altis.pbo")).Should().BeTrue();
    }

    [Fact]
    public void ArchiveMissionFile_ShouldCreateArchiveDirectoryWhenMissing()
    {
        var archiveDir = Path.Combine(_tempDir, "archive-missing");
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsPath()).Returns(_tempDir);
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsArchivePath()).Returns(archiveDir);
        File.WriteAllText(Path.Combine(_tempDir, "test.Altis.pbo"), "content");

        _subject.ArchiveMissionFile("test.Altis.pbo");

        Directory.Exists(archiveDir).Should().BeTrue();
        File.Exists(Path.Combine(archiveDir, "test.Altis.pbo")).Should().BeTrue();
    }

    [Fact]
    public void ArchiveMissionFile_ShouldThrowWhenFileNotInActivePath()
    {
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsPath()).Returns(_tempDir);

        var act = () => _subject.ArchiveMissionFile("nonexistent.pbo");

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void RestoreMissionFile_ShouldMoveFileFromArchiveToActive()
    {
        var archiveDir = Path.Combine(_tempDir, "archive");
        Directory.CreateDirectory(archiveDir);
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsPath()).Returns(_tempDir);
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsArchivePath()).Returns(archiveDir);
        File.WriteAllText(Path.Combine(archiveDir, "test.Altis.pbo"), "content");

        _subject.RestoreMissionFile("test.Altis.pbo");

        File.Exists(Path.Combine(archiveDir, "test.Altis.pbo")).Should().BeFalse();
        File.Exists(Path.Combine(_tempDir, "test.Altis.pbo")).Should().BeTrue();
    }

    [Fact]
    public void RestoreMissionFile_ShouldThrowWhenFileNotInArchivePath()
    {
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsArchivePath()).Returns(_tempDir);

        var act = () => _subject.RestoreMissionFile("nonexistent.pbo");

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void GetMissionFileStream_ShouldReturnStreamForExistingFile()
    {
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsPath()).Returns(_tempDir);
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsArchivePath()).Returns(Path.Combine(_tempDir, "archive"));
        File.WriteAllText(Path.Combine(_tempDir, "test.Altis.pbo"), "content");

        using var stream = _subject.GetMissionFileStream("test.Altis.pbo");

        stream.Should().NotBeNull();
        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public void GetMissionFileStream_ShouldThrowWhenFileNotFound()
    {
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsPath()).Returns(_tempDir);
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsArchivePath()).Returns(Path.Combine(_tempDir, "archive"));

        var act = () => _subject.GetMissionFileStream("nonexistent.pbo");

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void FindMissionFilePath_ShouldFindInActivePath()
    {
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsPath()).Returns(_tempDir);
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsArchivePath()).Returns(Path.Combine(_tempDir, "archive"));
        File.WriteAllText(Path.Combine(_tempDir, "test.Altis.pbo"), "");

        var result = _subject.FindMissionFilePath("test.Altis.pbo");

        result.Should().Be(Path.Combine(_tempDir, "test.Altis.pbo"));
    }

    [Fact]
    public void FindMissionFilePath_ShouldFindInArchivePath()
    {
        var archiveDir = Path.Combine(_tempDir, "archive");
        Directory.CreateDirectory(archiveDir);
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsPath()).Returns(_tempDir);
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsArchivePath()).Returns(archiveDir);
        File.WriteAllText(Path.Combine(archiveDir, "test.Altis.pbo"), "");

        var result = _subject.FindMissionFilePath("test.Altis.pbo");

        result.Should().Be(Path.Combine(archiveDir, "test.Altis.pbo"));
    }

    [Fact]
    public void FindMissionFilePath_ShouldReturnNullWhenNotFound()
    {
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsPath()).Returns(_tempDir);
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsArchivePath()).Returns(Path.Combine(_tempDir, "archive"));

        var result = _subject.FindMissionFilePath("nonexistent.pbo");

        result.Should().BeNull();
    }
}
