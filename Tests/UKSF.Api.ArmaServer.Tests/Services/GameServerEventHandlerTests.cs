using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerEventHandlerTests
{
    private readonly Mock<IGameServerProcessManager> _mockProcessManager = new();
    private readonly Mock<IGameServersContext> _mockContext = new();
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint = new();
    private readonly Mock<IMissionStatsService> _mockMissionStatsService = new();
    private readonly Mock<IPerformanceService> _mockPerformanceService = new();
    private readonly Mock<IPersistenceSessionsService> _mockPersistenceSessionsService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly GameServerEventHandler _sut;

    public GameServerEventHandlerTests()
    {
        _sut = new GameServerEventHandler(
            _mockProcessManager.Object,
            _mockContext.Object,
            _mockPublishEndpoint.Object,
            _mockMissionStatsService.Object,
            _mockPerformanceService.Object,
            _mockPersistenceSessionsService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task HandleEventAsync_ShutdownComplete_DelegatesToProcessManager()
    {
        var evt = new GameServerEvent
        {
            Type = "shutdown_complete",
            ApiPort = 2303,
            Data = new Dictionary<string, object>()
        };

        await _sut.HandleEventAsync(evt);

        _mockProcessManager.Verify(x => x.HandleShutdownCompleteAsync(2303), Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_ServerStatus_DelegatesToProcessManager()
    {
        var data = new Dictionary<string, object> { { "map", "Altis" } };
        var evt = new GameServerEvent
        {
            Type = "server_status",
            ApiPort = 2303,
            Data = data
        };

        await _sut.HandleEventAsync(evt);

        _mockProcessManager.Verify(x => x.HandleServerStatusAsync(2303, data), Times.Once);
    }

    [Fact]
    public async Task HandleEventAsync_UnknownType_LogsWarning()
    {
        var evt = new GameServerEvent { Type = "unknown_event", Data = new Dictionary<string, object>() };

        await _sut.HandleEventAsync(evt);

        _mockLogger.Verify(x => x.LogWarning(It.Is<string>(s => s.Contains("unknown_event"))), Times.Once);
    }
}
