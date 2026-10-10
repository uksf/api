using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerEventHandlerMissionEndTests
{
    private readonly Mock<IMissionStatsService> _missionStats = new();
    private readonly Mock<IUksfLogger> _logger = new();
    private readonly Mock<IGameServerProcessManager> _processManager = new();
    private readonly GameServerEventHandler _sut;

    public GameServerEventHandlerMissionEndTests()
    {
        _sut = new GameServerEventHandler(
            _processManager.Object,
            Mock.Of<IGameServersContext>(),
            Mock.Of<IPublishEndpoint>(),
            _missionStats.Object,
            Mock.Of<IPerformanceService>(),
            Mock.Of<IPersistenceSessionsService>(),
            _logger.Object,
            Mock.Of<INpcBrokerService>(),
            Mock.Of<IMissionSessionCaptureService>()
        );
    }

    private Task Send(Dictionary<string, object> data, string type = "mission_ended") =>
        _sut.HandleEventAsync(
            new GameServerEvent
            {
                Type = type,
                ApiPort = 2303,
                Data = data
            }
        );

    [Fact]
    public async Task MissionEnded_UsesTheExtensionEnqueueTime_SoAStatsBatchQueuedJustBeforeItIsKept()
    {
        await Send(
            new Dictionary<string, object>
            {
                { "sessionId", "s1" },
                { "duration", "60" },
                { "enqueueAt", "2026-10-09T18:55:29.352Z" }
            }
        );

        _missionStats.Verify(x => x.HandleMissionEndedAsync("s1", 60, new DateTime(2026, 10, 9, 18, 55, 29, 352, DateTimeKind.Utc)), Times.Once);
    }

    [Fact]
    public async Task MissionEnded_WithoutEnqueueTime_FallsBackToReceiveTime()
    {
        var before = DateTime.UtcNow;

        await Send(new Dictionary<string, object> { { "sessionId", "s1" }, { "duration", "60" } });

        _missionStats.Verify(x => x.HandleMissionEndedAsync("s1", 60, It.Is<DateTime>(t => t >= before && t <= DateTime.UtcNow)), Times.Once);
    }

    [Theory]
    [InlineData("lost")]
    [InlineData("rejected")]
    [InlineData("timeout")]
    [InlineData("unsent")]
    public async Task ShutdownStopping_WithAnUnstoredSave_LogsAnError(string outcome)
    {
        await Send(new Dictionary<string, object> { { "saveOutcome", outcome } }, "shutdown_stopping");

        _logger.Verify(x => x.LogError(It.Is<string>(message => message.Contains("2303") && message.Contains(outcome))), Times.Once);
    }

    [Fact]
    public async Task ShutdownStopping_WithADeliveredSave_LogsNoError()
    {
        await Send(new Dictionary<string, object> { { "saveOutcome", "delivered" } }, "shutdown_stopping");

        _logger.Verify(x => x.LogError(It.IsAny<string>()), Times.Never);
        _processManager.Verify(x => x.HandleStopStoppingAsync(2303), Times.Once);
    }

    [Fact]
    public async Task MissionEnded_WithoutASave_LogsAWarning()
    {
        await Send(
            new Dictionary<string, object>
            {
                { "sessionId", "s1" },
                { "duration", "60" },
                { "saved", false }
            }
        );

        _logger.Verify(x => x.LogWarning(It.Is<string>(message => message.Contains("s1"))), Times.Once);
    }

    [Fact]
    public async Task PersistenceSave_MarkedPartial_LogsAWarning()
    {
        await Send(
            new Dictionary<string, object>
            {
                { "key", "k" },
                { "sessionId", "s1" },
                { "partial", true },
                { "data", new Dictionary<string, object>() }
            },
            "persistence_save"
        );

        _logger.Verify(x => x.LogWarning(It.Is<string>(message => message.Contains("k") && message.Contains("partial"))), Times.Once);
    }

    [Fact]
    public async Task ShutdownDelivering_AdvancesTheStopPhase()
    {
        await Send(new Dictionary<string, object>(), "shutdown_delivering");

        _processManager.Verify(x => x.HandleStopDeliveringAsync(2303), Times.Once);
    }
}
