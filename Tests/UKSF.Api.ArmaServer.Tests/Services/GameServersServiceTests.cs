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
        await action.Should().ThrowAsync<InvalidOperationException>();
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

    [Fact]
    public async Task GetGameServerStatus_WhenSkipServerStatus_ShouldReplaceAndReturn()
    {
        var gameServer = new DomainGameServer { Id = "server-1", Port = 2302 };
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(true);

        await _subject.GetGameServerStatus(gameServer);

        _mockGameServersContext.Verify(x => x.Replace(gameServer), Times.Once);
        _mockGameServerHelpers.Verify(x => x.GetArmaProcessesWithCommandLine(), Times.Never);
        _mockHttpClientFactory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetGameServerStatus_WhenNoArmaProcesses_ShouldSetNotRunningWithoutHttpCall()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-1",
            Port = 2302,
            ProcessId = 1234
        };
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockGameServerHelpers.Setup(x => x.GetArmaProcessesWithCommandLine()).Returns([]);

        await _subject.GetGameServerStatus(gameServer);

        gameServer.Status.Running.Should().BeFalse();
        gameServer.Status.Started.Should().BeFalse();
        _mockHttpClientFactory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
        _mockGameServersContext.Verify(x => x.Replace(gameServer), Times.Once);
    }

    [Fact]
    public async Task GetGameServerStatus_WhenNoArmaProcesses_ShouldClearStaleProcessId()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-1",
            Port = 2302,
            ProcessId = 1234
        };
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockGameServerHelpers.Setup(x => x.GetArmaProcessesWithCommandLine()).Returns([]);

        await _subject.GetGameServerStatus(gameServer);

        gameServer.ProcessId.Should().BeNull();
    }

    [Fact]
    public async Task GetGameServerStatus_WhenArmaProcessesExistButNoPortMatch_ShouldSetNotRunning()
    {
        var gameServer = new DomainGameServer { Id = "server-1", Port = 2302 };
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockGameServerHelpers.Setup(x => x.GetArmaProcessesWithCommandLine()).Returns([new ProcessCommandLineInfo(5678, "-port=2402 -apiport=\"2403\"")]);

        await _subject.GetGameServerStatus(gameServer);

        gameServer.Status.Running.Should().BeFalse();
        gameServer.Status.Started.Should().BeFalse();
        _mockHttpClientFactory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetGameServerStatus_WhenArmaProcessMatchesPort_ShouldSetStartedAndMakeHttpCall()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-1",
            Port = 2302,
            ApiPort = 2303
        };
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockGameServerHelpers.Setup(x => x.GetArmaProcessesWithCommandLine()).Returns([new ProcessCommandLineInfo(5678, "-port=2302 -apiport=\"2303\"")]);

        var mockHandler = new MockHttpMessageHandler(System.Net.HttpStatusCode.RequestTimeout);
        var httpClient = new HttpClient(mockHandler);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        await _subject.GetGameServerStatus(gameServer);

        gameServer.Status.Started.Should().BeTrue();
        _mockHttpClientFactory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GetGameServerStatus_WhenArmaProcessMatchesPort_ShouldUpdateProcessId()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-1",
            Port = 2302,
            ApiPort = 2303,
            ProcessId = null
        };
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockGameServerHelpers.Setup(x => x.GetArmaProcessesWithCommandLine()).Returns([new ProcessCommandLineInfo(5678, "-port=2302 -apiport=\"2303\"")]);

        var mockHandler = new MockHttpMessageHandler(System.Net.HttpStatusCode.RequestTimeout);
        var httpClient = new HttpClient(mockHandler);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        await _subject.GetGameServerStatus(gameServer);

        gameServer.ProcessId.Should().Be(5678);
    }

    [Fact]
    public async Task GetGameServerStatus_WhenMultipleProcessesMatchPort_ShouldUseFirstNonHeadlessProcess()
    {
        var gameServer = new DomainGameServer
        {
            Id = "server-1",
            Port = 2302,
            ApiPort = 2303,
            ProcessId = null
        };
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockGameServerHelpers.Setup(x => x.GetArmaProcessesWithCommandLine())
                              .Returns(
                                  [
                                      new ProcessCommandLineInfo(1111, "-port=2302 -apiport=\"2303\" -config="),
                                      new ProcessCommandLineInfo(2222, "-port=2302 -apiport=\"2304\" -client")
                                  ]
                              );

        var mockHandler = new MockHttpMessageHandler(System.Net.HttpStatusCode.RequestTimeout);
        var httpClient = new HttpClient(mockHandler);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        await _subject.GetGameServerStatus(gameServer);

        gameServer.ProcessId.Should().Be(1111);
    }

    [Fact]
    public async Task GetAllGameServerStatuses_WhenNoArmaProcesses_ShouldSetAllNotRunningWithoutHttpCalls()
    {
        var gameServers = new List<DomainGameServer>
        {
            new()
            {
                Id = "server-1",
                Port = 2302,
                ProcessId = 1234
            },
            new()
            {
                Id = "server-2",
                Port = 2402,
                ProcessId = 5678
            }
        };
        _mockGameServersContext.Setup(x => x.Get()).Returns(gameServers);
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockGameServerHelpers.Setup(x => x.GetArmaProcessesWithCommandLine()).Returns([]);

        var result = await _subject.GetAllGameServerStatuses();

        result.Should()
              .AllSatisfy(server =>
                  {
                      server.Status.Running.Should().BeFalse();
                      server.Status.Started.Should().BeFalse();
                      server.ProcessId.Should().BeNull();
                  }
              );
        _mockHttpClientFactory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
        _mockGameServerHelpers.Verify(x => x.GetArmaProcessesWithCommandLine(), Times.Once);
    }

    [Fact]
    public async Task GetAllGameServerStatuses_WhenArmaProcesses_ShouldOnlyHttpCallMatchingServers()
    {
        var gameServers = new List<DomainGameServer>
        {
            new()
            {
                Id = "server-1",
                Port = 2302,
                ApiPort = 2303
            },
            new()
            {
                Id = "server-2",
                Port = 2402,
                ApiPort = 2403
            }
        };
        _mockGameServersContext.Setup(x => x.Get()).Returns(gameServers);
        _mockVariablesService.Setup(x => x.GetFeatureState("SKIP_SERVER_STATUS")).Returns(false);
        _mockGameServerHelpers.Setup(x => x.GetArmaProcessesWithCommandLine()).Returns([new ProcessCommandLineInfo(5678, "-port=2302 -apiport=\"2303\"")]);

        var mockHandler = new MockHttpMessageHandler(System.Net.HttpStatusCode.RequestTimeout);
        var httpClient = new HttpClient(mockHandler);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await _subject.GetAllGameServerStatuses();

        var server2 = result.Find(s => s.Id == "server-2");
        server2!.Status.Running.Should().BeFalse();
        server2.Status.Started.Should().BeFalse();
    }
}

internal class MockHttpMessageHandler(System.Net.HttpStatusCode statusCode) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(statusCode));
    }
}
