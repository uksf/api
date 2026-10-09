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
    private readonly GameServerEventHandler _sut;

    public GameServerEventHandlerMissionEndTests()
    {
        _sut = new GameServerEventHandler(
            Mock.Of<IGameServerProcessManager>(),
            Mock.Of<IGameServersContext>(),
            Mock.Of<IPublishEndpoint>(),
            _missionStats.Object,
            Mock.Of<IPerformanceService>(),
            Mock.Of<IPersistenceSessionsService>(),
            Mock.Of<IUksfLogger>(),
            Mock.Of<INpcBrokerService>(),
            Mock.Of<IMissionSessionCaptureService>()
        );
    }

    private Task Send(Dictionary<string, object> data) =>
        _sut.HandleEventAsync(
            new GameServerEvent
            {
                Type = "mission_ended",
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
}
