using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using MassTransit;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerEventHandlerSessionTests
{
    private readonly Mock<IMissionStatsService> _missionStats = new();
    private readonly Mock<IGameServerProcessManager> _processManager = new();
    private readonly Mock<IGameServersContext> _gameServers = new();
    private readonly GameServerEventHandler _sut;

    public GameServerEventHandlerSessionTests()
    {
        _gameServers.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(new DomainGameServer { Id = "server", ApiPort = 2303 });
        _sut = new GameServerEventHandler(
            _processManager.Object,
            _gameServers.Object,
            Mock.Of<IPublishEndpoint>(),
            _missionStats.Object,
            Mock.Of<IPerformanceService>(),
            Mock.Of<IPersistenceSessionsService>(),
            Mock.Of<IUksfLogger>(),
            Mock.Of<INpcBrokerService>(),
            Mock.Of<IMissionSessionCaptureService>()
        );
    }

    private Task Send(string type, string sessionId) =>
        _sut.HandleEventAsync(
            new GameServerEvent
            {
                Type = type,
                ApiPort = 2303,
                Data = new Dictionary<string, object>
                {
                    { "sessionId", sessionId },
                    { "mission", "op" },
                    { "map", "Altis" },
                    { "enqueueAt", "2026-10-14T12:00:00.000Z" }
                }
            }
        );

    [Fact]
    public async Task MissionStarted_ForASessionThatAlreadyEnded_DoesNotMakeItCurrent()
    {
        _missionStats.Setup(x => x.GetSessionAsync("old")).ReturnsAsync(new MissionSession { SessionId = "old", MissionEnded = DateTime.UtcNow });

        await Send("mission_started", "old");

        _processManager.Verify(x => x.RecordMissionSessionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<DateTime?>()), Times.Never);
    }

    [Fact]
    public async Task MissionStarted_ForANewSession_RecordsItWithItsEnqueueTime()
    {
        await Send("mission_started", "new");

        _processManager.Verify(x => x.RecordMissionSessionAsync(2303, "new", true, new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc)), Times.Once);
    }

    [Fact]
    public async Task MissionEnded_AsksTheProcessManagerToClearOnlyThatSession()
    {
        await Send("mission_ended", "old");

        _processManager.Verify(x => x.RecordMissionSessionAsync(2303, "old", false, It.IsAny<DateTime?>()), Times.Once);
        _gameServers.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<MongoDB.Driver.UpdateDefinition<DomainGameServer>>()), Times.Never);
    }

    [Theory]
    [InlineData("mission_ended")]
    [InlineData("shutdown_saving")]
    public async Task AcknowledgedEvent_WhenHandlingFails_PropagatesTheFailure(string type)
    {
        _processManager.Setup(x => x.RecordMissionSessionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<DateTime?>()))
                       .ThrowsAsync(new TimeoutException("mongo"));
        _processManager.Setup(x => x.HandleStopSavingAsync(It.IsAny<int>(), It.IsAny<DateTime?>())).ThrowsAsync(new TimeoutException("mongo"));

        var act = () => Send(type, "old");

        await act.Should().ThrowAsync<TimeoutException>();
    }
}
