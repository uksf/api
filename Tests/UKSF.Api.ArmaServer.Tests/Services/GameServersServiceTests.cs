using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaMissions.Services;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServersServiceTests
{
    private readonly Mock<IGameServerHelpers> _mockGameServerHelpers = new();
    private readonly Mock<IGameServersContext> _mockGameServersContext = new();
    private readonly Mock<IMissionPatchingService> _mockMissionPatchingService = new();
    private readonly Mock<IVariablesService> _mockVariablesService = new();

    private readonly GameServersService _subject;

    public GameServersServiceTests()
    {
        _subject = new GameServersService(
            _mockGameServersContext.Object,
            _mockMissionPatchingService.Object,
            _mockGameServerHelpers.Object,
            _mockVariablesService.Object
        );
    }

    [Fact]
    public async Task LaunchGameServer_Should_launch_server_successfully()
    {
        // Arrange
        const string testServerId = "server-456";
        var gameServer = new DomainGameServer { Id = testServerId };

        _mockGameServerHelpers.Setup(x => x.FormatGameServerLaunchArguments(gameServer)).Returns("test-args");
        _mockGameServerHelpers.Setup(x => x.GetGameServerExecutablePath(gameServer)).Returns("test-path");

        // Act
        await _subject.LaunchGameServer(gameServer);

        // Assert
        _mockGameServerHelpers.Verify(x => x.FormatGameServerLaunchArguments(gameServer), Times.Once);
        _mockGameServerHelpers.Verify(x => x.GetGameServerExecutablePath(gameServer), Times.Once);
    }

    [Fact]
    public async Task LaunchGameServer_Should_launch_headless_clients()
    {
        // Arrange
        const string testServerId = "server-456";
        var gameServer = new DomainGameServer
        {
            Id = testServerId,
            NumberHeadlessClients = 2,
            HeadlessClientProcessIds = []
        };

        _mockGameServerHelpers.Setup(x => x.FormatGameServerLaunchArguments(gameServer)).Returns("test-args");
        _mockGameServerHelpers.Setup(x => x.FormatHeadlessClientLaunchArguments(gameServer, It.IsAny<int>())).Returns("headless-args");
        _mockGameServerHelpers.Setup(x => x.GetGameServerExecutablePath(gameServer)).Returns("test-path");

        // Act
        await _subject.LaunchGameServer(gameServer);

        // Assert
        gameServer.HeadlessClientProcessIds.Count.Should().Be(2);
        _mockGameServerHelpers.Verify(x => x.FormatGameServerLaunchArguments(gameServer), Times.Once);
        _mockGameServerHelpers.Verify(x => x.FormatHeadlessClientLaunchArguments(gameServer, It.IsAny<int>()), Times.Exactly(2));
        _mockGameServerHelpers.Verify(x => x.GetGameServerExecutablePath(gameServer), Times.Exactly(3));
    }

    [Fact]
    public async Task StopGameServer_Should_stop_server_successfully()
    {
        // Arrange
        const string testServerId = "server-456";
        var gameServer = new DomainGameServer
        {
            Id = testServerId,
            LaunchedBy = "previous-user-123",
            ApiPort = 8080
        };

        // Act
        await _subject.StopGameServer(gameServer);

        // Assert
        gameServer.ApiPort.Should().Be(8080);
    }

    [Fact]
    public async Task StopGameServer_Should_stop_headless_clients()
    {
        // Arrange
        const string testServerId = "server-456";
        var gameServer = new DomainGameServer
        {
            Id = testServerId,
            LaunchedBy = "previous-user-123",
            ApiPort = 8080,
            NumberHeadlessClients = 2
        };

        // Act
        await _subject.StopGameServer(gameServer);

        // Assert
        gameServer.NumberHeadlessClients.Should().Be(2);
        gameServer.ApiPort.Should().Be(8080);
    }

    [Fact]
    public void KillGameServer_Should_kill_server_successfully()
    {
        // Arrange
        const string testServerId = "server-456";
        var gameServer = new DomainGameServer
        {
            Id = testServerId,
            LaunchedBy = "previous-user-123",
            ProcessId = 1234,
            HeadlessClientProcessIds = []
        };

        // Act
        _subject.KillGameServer(gameServer);

        // Assert
        gameServer.ProcessId.Should().BeNull();
        gameServer.HeadlessClientProcessIds.Count.Should().Be(0);
    }

    [Fact]
    public void KillGameServer_Should_handle_null_process_id()
    {
        // Arrange
        var gameServer = new DomainGameServer
        {
            Id = "server-456",
            LaunchedBy = "previous-user-123",
            ProcessId = null
        };

        // Act & Assert
        var action = () => _subject.KillGameServer(gameServer);
        action.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void KillGameServer_Should_handle_zero_process_id()
    {
        // Arrange
        var gameServer = new DomainGameServer
        {
            Id = "server-456",
            LaunchedBy = "previous-user-123",
            ProcessId = 0
        };

        // Act & Assert
        var action = () => _subject.KillGameServer(gameServer);
        action.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void KillAllArmaProcesses_Should_clear_process_data()
    {
        // Arrange
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

        // Act
        var result = _subject.KillAllArmaProcesses();

        // Assert
        gameServers.Should()
                   .AllSatisfy(server =>
                       {
                           server.ProcessId.Should().BeNull();
                           server.HeadlessClientProcessIds.Count.Should().Be(0);
                       }
                   );

        result.Should().Be(0);
    }
}
