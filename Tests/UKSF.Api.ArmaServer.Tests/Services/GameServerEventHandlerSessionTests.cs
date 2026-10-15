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
    private readonly Mock<IMissionSessionCaptureService> _capture = new();
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
            _capture.Object
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

    private void ProcessManagerDecides(bool accept) =>
        _processManager.Setup(x => x.RecordMissionSessionAsync(
                                  It.IsAny<int>(),
                                  It.IsAny<string>(),
                                  It.IsAny<bool>(),
                                  It.IsAny<DateTime?>(),
                                  It.IsAny<Func<Task>>()
                              )
                       )
                       .Returns<int, string, bool, DateTime?, Func<Task>>(async (_, _, started, _, effects) =>
                           {
                               if (accept || !started)
                               {
                                   await effects();
                               }

                               return accept;
                           }
                       );

    [Fact]
    public async Task MissionStarted_WhenTheProcessManagerRejectsIt_SkipsEveryOtherStartEffect()
    {
        ProcessManagerDecides(false);

        await Send("mission_started", "old");

        _missionStats.Verify(x => x.HandleMissionStartedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        _capture.Verify(x => x.CaptureStartedAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task MissionStarted_WithoutAServerRow_ForAnEndedSession_IsSkipped()
    {
        _gameServers.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns((DomainGameServer)null);
        _missionStats.Setup(x => x.GetSessionAsync("old")).ReturnsAsync(new MissionSession { SessionId = "old", MissionEnded = DateTime.UtcNow });

        await Send("mission_started", "old");

        _missionStats.Verify(x => x.HandleMissionStartedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task MissionStarted_ForANewSession_RecordsItWithItsEnqueueTime()
    {
        ProcessManagerDecides(true);

        await Send("mission_started", "new");

        _processManager.Verify(
            x => x.RecordMissionSessionAsync(2303, "new", true, new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc), It.IsAny<Func<Task>>()),
            Times.Once
        );
        _missionStats.Verify(x => x.HandleMissionStartedAsync("new", "op", "Altis", It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task MissionEnded_EndsTheStatisticsSessionThroughTheProcessManager()
    {
        ProcessManagerDecides(true);

        await Send("mission_ended", "old");

        _missionStats.Verify(x => x.HandleMissionEndedAsync("old", It.IsAny<double>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task MissionEnded_AsksTheProcessManagerToClearOnlyThatSession()
    {
        await Send("mission_ended", "old");

        _processManager.Verify(x => x.RecordMissionSessionAsync(2303, "old", false, It.IsAny<DateTime?>(), It.IsAny<Func<Task>>()), Times.Once);
        _gameServers.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<MongoDB.Driver.UpdateDefinition<DomainGameServer>>()), Times.Never);
    }

    [Theory]
    [InlineData("mission_ended")]
    [InlineData("shutdown_saving")]
    public async Task AcknowledgedEvent_WhenHandlingFails_PropagatesTheFailure(string type)
    {
        _processManager.Setup(x => x.RecordMissionSessionAsync(
                                  It.IsAny<int>(),
                                  It.IsAny<string>(),
                                  It.IsAny<bool>(),
                                  It.IsAny<DateTime?>(),
                                  It.IsAny<Func<Task>>()
                              )
                       )
                       .ThrowsAsync(new TimeoutException("mongo"));
        _processManager.Setup(x => x.HandleStopSavingAsync(It.IsAny<int>(), It.IsAny<DateTime?>())).ThrowsAsync(new TimeoutException("mongo"));

        var act = () => Send(type, "old");

        await act.Should().ThrowAsync<TimeoutException>();
    }
}
