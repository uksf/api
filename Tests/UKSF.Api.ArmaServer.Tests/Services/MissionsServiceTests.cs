using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Models;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class MissionsServiceTests
{
    private readonly Mock<IMissionPatchingService> _mockMissionPatchingService = new();
    private readonly Mock<IGameServerHelpers> _mockGameServerHelpers = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();

    private readonly MissionsService _subject;

    public MissionsServiceTests()
    {
        _subject = new MissionsService(_mockMissionPatchingService.Object, _mockGameServerHelpers.Object, _mockLogger.Object);
    }

    [Fact]
    public void GetActiveMissionsPath_ShouldReturnPathFromHelpers()
    {
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsPath()).Returns(@"C:\missions\active");

        var result = _subject.GetActiveMissionsPath();

        result.Should().Be(@"C:\missions\active");
    }

    [Fact]
    public void GetArchivedMissionsPath_ShouldReturnPathFromHelpers()
    {
        _mockGameServerHelpers.Setup(x => x.GetGameServerMissionsArchivePath()).Returns(@"C:\missions\archive");

        var result = _subject.GetArchivedMissionsPath();

        result.Should().Be(@"C:\missions\archive");
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
}
