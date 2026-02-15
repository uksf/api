using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Processes;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServersServiceTests
{
    private readonly Mock<IGameServerHelpers> _mockGameServerHelpers = new();
    private readonly Mock<IGameServersContext> _mockGameServersContext = new();
    private readonly Mock<IMissionPatchingService> _mockMissionPatchingService = new();
    private readonly Mock<IProcessUtilities> _mockProcessUtilities = new();
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();

    private readonly GameServersService _subject;

    public GameServersServiceTests()
    {
        _subject = new GameServersService(
            _mockGameServersContext.Object,
            _mockMissionPatchingService.Object,
            _mockGameServerHelpers.Object,
            _mockProcessUtilities.Object,
            _mockHttpClientFactory.Object,
            _mockVariablesService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task LaunchGameServer_Should_launch_server_successfully()
    {
        const string TestServerId = "server-456";
        var gameServer = new DomainGameServer { Id = TestServerId };

        _mockGameServerHelpers.Setup(x => x.FormatGameServerLaunchArguments(gameServer)).Returns("test-args");
        _mockGameServerHelpers.Setup(x => x.GetGameServerExecutablePath(gameServer)).Returns("test-path");
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("test-path", "test-args")).Returns(1234);

        await _subject.LaunchGameServer(gameServer);

        gameServer.ProcessId.Should().Be(1234);
        _mockProcessUtilities.Verify(x => x.LaunchManagedProcess("test-path", "test-args"), Times.Once);
    }

    [Fact]
    public async Task LaunchGameServer_Should_launch_headless_clients()
    {
        const string TestServerId = "server-456";
        var gameServer = new DomainGameServer
        {
            Id = TestServerId,
            NumberHeadlessClients = 2,
            HeadlessClientProcessIds = []
        };

        _mockGameServerHelpers.Setup(x => x.FormatGameServerLaunchArguments(gameServer)).Returns("test-args");
        _mockGameServerHelpers.Setup(x => x.FormatHeadlessClientLaunchArguments(gameServer, It.IsAny<int>())).Returns("headless-args");
        _mockGameServerHelpers.Setup(x => x.GetGameServerExecutablePath(gameServer)).Returns("test-path");
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("test-path", It.IsAny<string>())).Returns(5678);

        await _subject.LaunchGameServer(gameServer);

        gameServer.HeadlessClientProcessIds.Count.Should().Be(2);
        _mockProcessUtilities.Verify(x => x.LaunchManagedProcess("test-path", "test-args"), Times.Once);
        _mockProcessUtilities.Verify(x => x.LaunchManagedProcess("test-path", "headless-args"), Times.Exactly(2));
    }

    [Fact]
    public async Task KillGameServer_Should_kill_server_successfully()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-456",
            LaunchedBy = "previous-user-123",
            ProcessId = 1234,
            HeadlessClientProcessIds = []
        };

        _mockProcessUtilities.Setup(x => x.FindProcessById(1234)).Returns((System.Diagnostics.Process)null);

        await _subject.KillGameServer(gameServer);

        gameServer.ProcessId.Should().BeNull();
        gameServer.HeadlessClientProcessIds.Count.Should().Be(0);
        _mockProcessUtilities.Verify(x => x.FindProcessById(1234), Times.Once);
    }

    [Fact]
    public async Task KillGameServer_ShouldClearLaunchedBy()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-456",
            LaunchedBy = "user-123",
            ProcessId = 1234,
            HeadlessClientProcessIds = []
        };

        _mockProcessUtilities.Setup(x => x.FindProcessById(1234)).Returns((System.Diagnostics.Process)null);

        await _subject.KillGameServer(gameServer);

        gameServer.LaunchedBy.Should().BeNull();
    }

    [Fact]
    public async Task KillGameServer_Should_handle_null_process_id()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-456",
            LaunchedBy = "previous-user-123",
            ProcessId = null
        };

        var action = () => _subject.KillGameServer(gameServer);
        await action.Should().ThrowAsync<NullReferenceException>();
    }

    [Fact]
    public async Task KillAllArmaProcesses_Should_clear_process_data()
    {
        var gameServers = new List<DomainGameServer>
        {
            new()
            {
                Id = "server1",
                LaunchedBy = "user1",
                ProcessId = 1001,
                HeadlessClientProcessIds = []
            },
            new()
            {
                Id = "server2",
                LaunchedBy = "user2",
                ProcessId = 1002,
                HeadlessClientProcessIds = []
            },
            new()
            {
                Id = "server3",
                LaunchedBy = null,
                ProcessId = null,
                HeadlessClientProcessIds = []
            }
        };

        _mockGameServersContext.Setup(x => x.Get()).Returns(gameServers);
        _mockGameServerHelpers.Setup(x => x.GetArmaProcesses()).Returns([]);

        var result = await _subject.KillAllArmaProcesses();

        gameServers.Should()
                   .AllSatisfy(server =>
                       {
                           server.ProcessId.Should().BeNull();
                           server.HeadlessClientProcessIds.Count.Should().Be(0);
                       }
                   );
        result.Should().Be(0);
    }

    [Fact]
    public async Task KillAllArmaProcesses_ShouldClearLaunchedBy()
    {
        var gameServers = new List<DomainGameServer>
        {
            new()
            {
                Id = "server1",
                LaunchedBy = "user1",
                ProcessId = 1001,
                HeadlessClientProcessIds = []
            },
            new()
            {
                Id = "server2",
                LaunchedBy = "user2",
                ProcessId = 1002,
                HeadlessClientProcessIds = []
            }
        };

        _mockGameServersContext.Setup(x => x.Get()).Returns(gameServers);
        _mockGameServerHelpers.Setup(x => x.GetArmaProcesses()).Returns([]);

        await _subject.KillAllArmaProcesses();

        gameServers.Should().AllSatisfy(server => server.LaunchedBy.Should().BeNull());
    }

    [Fact]
    public async Task LaunchGameServer_ShouldSetMissionAndLaunchedBy()
    {
        var gameServer = new DomainGameServer { Id = "server-1" };

        _mockGameServerHelpers.Setup(x => x.FormatGameServerLaunchArguments(gameServer)).Returns("args");
        _mockGameServerHelpers.Setup(x => x.GetGameServerExecutablePath(gameServer)).Returns("path");
        _mockProcessUtilities.Setup(x => x.LaunchManagedProcess("path", "args")).Returns(1234);

        await _subject.LaunchGameServer(gameServer, "mission.pbo", "user-123");

        gameServer.Status.Mission.Should().Be("mission.pbo");
        gameServer.LaunchedBy.Should().Be("user-123");
    }
}
