using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Models;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerProcessManagerStopTests : GameServerProcessManagerTestBase
{
    [Fact]
    public async Task StopServerAsync_SetsProvisionalEndingAndPushes()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ApiPort = 2303,
            Status = new GameServerStatus { Running = true }
        };
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.StopServerAsync(server);

        server.Status.StopPhase.Should().Be(StopPhase.Ending);
        server.Status.StopRequestedAt.Should().NotBeNull();
        server.Status.StopPhaseEnteredAt.Should().BeNull(); // provisional, not armed until a game event
        _mockContext.Verify(x => x.Replace(server), Times.Once);
        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.IsAny<GameServerUpdate>()), Times.Once);
    }

    [Fact]
    public async Task StopServerAsync_SetsShortTimeoutOnShutdownRequest_ToAvoidBlockingTheServerLock()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ApiPort = 2303,
            Status = new GameServerStatus { Running = true }
        };
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);
        var httpClient = new HttpClient(new MockHttpMessageHandler(HttpStatusCode.OK));
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        await _sut.StopServerAsync(server);

        httpClient.Timeout.Should().Be(TimeSpan.FromSeconds(5)); // matches UpdateServerStatus's timeout for the same class of call
    }

    [Fact]
    public async Task StopServerAsync_WhenNotRunning_KillsInstead()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = 1234,
            HeadlessClientProcessIds = [],
            Status = new GameServerStatus { Running = false, Launching = true }
        };
        _mockProcessUtilities.Setup(x => x.FindProcessById(1234)).Returns((Process)null);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.StopServerAsync(server);

        server.ProcessId.Should().BeNull();
        server.Status.Running.Should().BeFalse();
        server.Status.Launching.Should().BeFalse();
    }

    [Fact]
    public async Task HandleStopEndingAsync_SetsEndingArmedAndPushes()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ApiPort = 2303,
            ProcessId = 1234,
            HeadlessClientProcessIds = [5001],
            Status = new GameServerStatus { Running = true, CurrentMissionSessionId = "sess-1" }
        };
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.HandleStopEndingAsync(2303);

        server.Status.StopPhase.Should().Be(StopPhase.Ending);
        server.Status.StopPhaseEnteredAt.Should().NotBeNull(); // armed
        server.Status.StopRequestedAt.Should().NotBeNull(); // set for in-game path
        // Must NOT clear process/session state:
        server.ProcessId.Should().Be(1234);
        server.HeadlessClientProcessIds.Should().Contain(5001);
        server.Status.CurrentMissionSessionId.Should().Be("sess-1");
        _mockMissionStatsService.Verify(x => x.FinaliseKilledSessionAsync(It.IsAny<string>()), Times.Never);
        _mockContext.Verify(x => x.Replace(server), Times.Once);
        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.IsAny<GameServerUpdate>()), Times.Once);
    }

    [Fact]
    public async Task HandleStopEndingAsync_PreservesExistingStopRequestedAt()
    {
        var requested = DateTime.UtcNow.AddSeconds(-3);
        var server = new DomainGameServer
        {
            Id = "s1",
            ApiPort = 2303,
            Status = new GameServerStatus { StopPhase = StopPhase.Ending, StopRequestedAt = requested }
        };
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.HandleStopEndingAsync(2303);

        server.Status.StopRequestedAt.Should().Be(requested); // web-press value preserved, not overwritten
        server.Status.StopPhaseEnteredAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AdvanceStopPhaseAsync_NeverRegressesPhase()
    {
        var enteredAt = DateTime.UtcNow.AddSeconds(-30);
        var server = new DomainGameServer
        {
            Id = "s1",
            ApiPort = 2303,
            Status = new GameServerStatus
            {
                StopPhase = StopPhase.Saving,
                StopPhaseEnteredAt = enteredAt,
                StopRequestedAt = enteredAt
            }
        };
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.HandleStopEndingAsync(2303); // late/duplicated ending event, arrives during Saving

        server.Status.StopPhase.Should().Be(StopPhase.Saving);
        server.Status.StopPhaseEnteredAt.Should().Be(enteredAt); // 120s Saving ceiling clock untouched
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Never);
        _mockServersClient.Verify(x => x.ReceiveServerUpdate(It.IsAny<GameServerUpdate>()), Times.Never);
    }

    [Fact]
    public async Task HandleStopSavingAsync_SetsSavingArmedWithoutClearingState()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            ApiPort = 2303,
            ProcessId = 1234,
            HeadlessClientProcessIds = [5001],
            Status = new GameServerStatus
            {
                StopPhase = StopPhase.Ending,
                StopRequestedAt = DateTime.UtcNow.AddSeconds(-4),
                CurrentMissionSessionId = "sess-1"
            }
        };
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.HandleStopSavingAsync(2303);

        server.Status.StopPhase.Should().Be(StopPhase.Saving);
        server.Status.StopPhaseEnteredAt.Should().NotBeNull();
        server.ProcessId.Should().Be(1234);
        server.HeadlessClientProcessIds.Should().Contain(5001);
        server.Status.CurrentMissionSessionId.Should().Be("sess-1");
        _mockMissionStatsService.Verify(x => x.FinaliseKilledSessionAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleStopStoppingAsync_SetsStoppingArmedWithoutClearingState()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            ApiPort = 2303,
            ProcessId = 1234,
            HeadlessClientProcessIds = [5001],
            Status = new GameServerStatus
            {
                StopPhase = StopPhase.Saving,
                StopRequestedAt = DateTime.UtcNow.AddSeconds(-8),
                CurrentMissionSessionId = "sess-1"
            }
        };
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.HandleStopStoppingAsync(2303);

        server.Status.StopPhase.Should().Be(StopPhase.Stopping);
        server.Status.StopPhaseEnteredAt.Should().NotBeNull();
        server.ProcessId.Should().Be(1234); // NOT cleared — OS-death owns that
        server.HeadlessClientProcessIds.Should().Contain(5001);
        server.Status.CurrentMissionSessionId.Should().Be("sess-1");
        _mockMissionStatsService.Verify(x => x.FinaliseKilledSessionAsync(It.IsAny<string>()), Times.Never);
        _mockOpSessionCaptureService.Verify(x => x.CaptureEndedAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleStopStoppingAsync_WhenNoMatchingServer_LogsWarning()
    {
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns((DomainGameServer)null);

        await _sut.HandleStopStoppingAsync(9999);

        _mockLogger.Verify(x => x.LogWarning(It.Is<string>(s => s.Contains("9999"))), Times.Once);
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Never);
    }
}
