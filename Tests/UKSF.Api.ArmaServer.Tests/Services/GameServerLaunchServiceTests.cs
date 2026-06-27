using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core.Exceptions;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerLaunchServiceTests
{
    private readonly Mock<IGameServersService> _mockServers = new();
    private readonly Mock<IMissionsService> _mockMissions = new();
    private readonly Mock<IGameServerProcessManager> _mockProcess = new();
    private readonly Mock<IGameServerHelpers> _mockHelpers = new();
    private readonly GameServerLaunchService _service;

    public GameServerLaunchServiceTests()
    {
        _service = new GameServerLaunchService(_mockServers.Object, _mockMissions.Object, _mockProcess.Object, _mockHelpers.Object);
    }

    [Fact]
    public async Task Throws_when_server_already_running()
    {
        DomainGameServer server = new() { Id = "s1", Status = new GameServerStatus { Running = true } };
        _mockServers.Setup(x => x.GetServer("s1")).Returns(server);

        var act = () => _service.LaunchAsync("s1", "m.Altis.pbo", "user1");

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task Patches_and_launches_on_happy_path()
    {
        DomainGameServer server = new() { Id = "s1", Port = 2302, Status = new GameServerStatus() };
        _mockServers.Setup(x => x.GetServer("s1")).Returns(server);
        _mockServers.Setup(x => x.GetServers()).Returns([server]);
        _mockHelpers.Setup(x => x.IsMainOpTime()).Returns(false);
        _mockMissions.Setup(x => x.PatchMissionFile("m.Altis.pbo"))
                     .ReturnsAsync(new MissionPatchingResult { Success = true, PlayerCount = 42, Reports = [] });

        var reports = await _service.LaunchAsync("s1", "m.Altis.pbo", "user1");

        reports.Should().BeEmpty();
        _mockProcess.Verify(x => x.LaunchServerAsync(server, "m.Altis.pbo", "user1", 42), Times.Once);
    }
}
