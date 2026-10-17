using System;
using System.Collections.Generic;
using System.Linq;
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
            Status = new GameServerStatus { Running = true, CurrentMissionSessionId = "sess-1" }
        };
        _servers.Add(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.StopServerAsync(server);

        server.Status.StopPhase.Should().Be(StopPhase.Ending);
        server.Status.StopRequestedAt.Should().NotBeNull();
        server.Status.StopPhaseEnteredAt.Should().BeNull();
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
            Status = new GameServerStatus { Running = true, CurrentMissionSessionId = "sess-1" }
        };
        _servers.Add(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);
        var httpClient = new HttpClient(new MockHttpMessageHandler(HttpStatusCode.OK));
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        await _sut.StopServerAsync(server);

        httpClient.Timeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task StopServerAsync_WhenNotRunning_KillsInstead()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            ProcessId = 1234,
            HeadlessClients = [],
            Status = new GameServerStatus { Running = false, Launching = true }
        };
        _servers.Add(server);
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
            HeadlessClients = [new(5001, HeadlessClientStart)],
            Status = new GameServerStatus { Running = true, CurrentMissionSessionId = "sess-1" }
        };
        _servers.Add(server);
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.HandleStopEndingAsync(2303);

        server.Status.StopPhase.Should().Be(StopPhase.Ending);
        server.Status.StopPhaseEnteredAt.Should().NotBeNull();
        server.Status.StopRequestedAt.Should().NotBeNull();
        server.ProcessId.Should().Be(1234);
        server.HeadlessClients.Select(x => x.ProcessId).Should().Contain(5001);
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
        _servers.Add(server);
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.HandleStopEndingAsync(2303);

        server.Status.StopRequestedAt.Should().Be(requested);
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
        _servers.Add(server);
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.HandleStopEndingAsync(2303);

        server.Status.StopPhase.Should().Be(StopPhase.Saving);
        server.Status.StopPhaseEnteredAt.Should().Be(enteredAt);
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
            HeadlessClients = [new(5001, HeadlessClientStart)],
            Status = new GameServerStatus
            {
                StopPhase = StopPhase.Ending,
                StopRequestedAt = DateTime.UtcNow.AddSeconds(-4),
                CurrentMissionSessionId = "sess-1"
            }
        };
        _servers.Add(server);
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.HandleStopSavingAsync(2303);

        server.Status.StopPhase.Should().Be(StopPhase.Saving);
        server.Status.StopPhaseEnteredAt.Should().NotBeNull();
        server.ProcessId.Should().Be(1234);
        server.HeadlessClients.Select(x => x.ProcessId).Should().Contain(5001);
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
            HeadlessClients = [new(5001, HeadlessClientStart)],
            Status = new GameServerStatus
            {
                StopPhase = StopPhase.Saving,
                StopRequestedAt = DateTime.UtcNow.AddSeconds(-8),
                CurrentMissionSessionId = "sess-1"
            }
        };
        _servers.Add(server);
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.HandleStopStoppingAsync(2303);

        server.Status.StopPhase.Should().Be(StopPhase.Stopping);
        server.Status.StopPhaseEnteredAt.Should().NotBeNull();
        server.ProcessId.Should().Be(1234);
        server.HeadlessClients.Select(x => x.ProcessId).Should().Contain(5001);
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

    [Fact]
    public async Task HandleStopDeliveringAsync_AdvancesFromSaving()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            ApiPort = 2303,
            Status = new GameServerStatus { StopPhase = StopPhase.Saving, StopPhaseEnteredAt = DateTime.UtcNow }
        };
        _servers.Add(server);
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.HandleStopDeliveringAsync(2303);

        server.Status.StopPhase.Should().Be(StopPhase.Delivering);
        _mockContext.Verify(x => x.Replace(server), Times.Once);
    }

    [Fact]
    public async Task HandleStopDeliveringAsync_ArrivingAfterStopping_DoesNotRegress()
    {
        var enteredAt = DateTime.UtcNow.AddSeconds(-2);
        var server = new DomainGameServer
        {
            Id = "s1",
            ApiPort = 2303,
            Status = new GameServerStatus { StopPhase = StopPhase.Stopping, StopPhaseEnteredAt = enteredAt }
        };
        _servers.Add(server);
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.HandleStopDeliveringAsync(2303);

        server.Status.StopPhase.Should().Be(StopPhase.Stopping);
        server.Status.StopPhaseEnteredAt.Should().Be(enteredAt);
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Never);
    }

    [Fact]
    public async Task HandleStopStoppingAsync_AfterDelivering_Advances()
    {
        var server = new DomainGameServer
        {
            Id = "s1",
            ApiPort = 2303,
            Status = new GameServerStatus { StopPhase = StopPhase.Delivering, StopPhaseEnteredAt = DateTime.UtcNow }
        };
        _servers.Add(server);
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);

        await _sut.HandleStopStoppingAsync(2303);

        server.Status.StopPhase.Should().Be(StopPhase.Stopping);
        _mockContext.Verify(x => x.Replace(server), Times.Once);
    }
}
