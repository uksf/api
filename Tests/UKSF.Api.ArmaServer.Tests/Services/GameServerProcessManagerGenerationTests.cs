using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core.Processes;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerProcessManagerGenerationTests : GameServerProcessManagerTestBase
{
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly DateTime _processStart;

    public GameServerProcessManagerGenerationTests()
    {
        _processStart = GameServerProcessManager.CanonicalStartTime(_process.StartTime);
        _mockProcessUtilities.Setup(x => x.FindProcessById(_process.Id)).Returns(_process);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);
    }

    private DomainGameServer Server(GameServerStatus status = null) =>
        new()
        {
            Id = "s1",
            Name = "Test",
            Port = 2302,
            ApiPort = 2303,
            ProcessId = _process.Id,
            ProcessStartedAt = _processStart,
            Status = status ?? new GameServerStatus { Running = true }
        };

    [Fact]
    public async Task Monitor_WhenTheRecordMovedToANewProcessAfterTheSnapshot_LeavesTheNewProcessAlone()
    {
        var stale = Server();
        stale.ProcessId = 1;
        var current = Server();
        _servers.Add(current);
        var calls = 0;
        _mockContext.Setup(x => x.Get()).Returns(() => ++calls == 1 ? [stale] : new List<DomainGameServer>());

        _sut.EnsureMonitorRunning();
        await Task.Delay(1000);

        current.ProcessId.Should().Be(_process.Id);
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Never);
    }

    [Fact]
    public async Task KillServer_WhenAStoredHeadlessClientPidWasReused_DoesNotKillTheStranger()
    {
        var server = Server(new GameServerStatus());
        server.ProcessId = null;
        server.ProcessStartedAt = null;
        server.HeadlessClients = [new TrackedProcess(_process.Id, _processStart.AddMinutes(-10))];
        _servers.Add(server);

        await _sut.KillServerAsync(server);

        _mockProcessUtilities.Verify(x => x.KillProcess(It.IsAny<Process>(), It.IsAny<bool>()), Times.Never);
        server.HeadlessClients.Should().BeEmpty();
    }

    [Fact]
    public async Task KillAll_SkipsAListedProcessThatAnotherApiOwns()
    {
        _mockContext.Setup(x => x.Get()).Returns(new List<DomainGameServer>());
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(_process.Id, "")]);
        _mockHelpers.Setup(x => x.IsOwnedProcessId(_process.Id)).Returns(false);

        var killed = await _sut.KillAllAsync();

        killed.Should().Be(0);
        _mockProcessUtilities.Verify(x => x.KillProcess(It.IsAny<Process>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task RecordMissionSession_WhenAnOlderSessionEnds_KeepsTheCurrentSession()
    {
        var server = Server(new GameServerStatus { Running = true, CurrentMissionSessionId = "B" });
        _servers.Add(server);
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);

        await _sut.RecordMissionSessionAsync(2303, "A", started: false, null, () => Task.CompletedTask);

        server.Status.CurrentMissionSessionId.Should().Be("B");
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainGameServer>()), Times.Never);
    }

    [Fact]
    public async Task RecordMissionSession_WhenTheCurrentSessionEnds_ClearsIt()
    {
        var server = Server(new GameServerStatus { Running = true, CurrentMissionSessionId = "B" });
        _servers.Add(server);
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);

        await _sut.RecordMissionSessionAsync(2303, "B", started: false, null, () => Task.CompletedTask);

        server.Status.CurrentMissionSessionId.Should().BeNull();
    }

    [Fact]
    public async Task HandleServerStatus_DoesNotRestoreACachedSessionOverTheRecordedOne()
    {
        var server = Server(new GameServerStatus { Running = true, CurrentMissionSessionId = "A" });
        _servers.Add(server);
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);
        await _sut.HandleServerStatusAsync(2303, new Dictionary<string, object>());

        server.Status = new GameServerStatus { Running = true, CurrentMissionSessionId = "B" };
        await _sut.HandleServerStatusAsync(2303, new Dictionary<string, object>());

        server.Status.CurrentMissionSessionId.Should().Be("B");
    }

    [Fact]
    public async Task StopPhaseEvent_SentBeforeTheCurrentProcessStarted_IsIgnored()
    {
        var server = Server();
        _servers.Add(server);
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>())).Returns(server);

        await _sut.HandleStopSavingAsync(2303, _processStart.AddMinutes(-1));

        server.Status.StopPhase.Should().Be(StopPhase.None);
    }

    [Fact]
    public async Task Monitor_WhenShutdownIsUnacknowledged_SendsItAgain()
    {
        var server = Server(
            new GameServerStatus
            {
                Running = true,
                StopPhase = StopPhase.Ending,
                StopRequestedAt = DateTime.UtcNow
            }
        );
        _servers.Add(server);
        _mockContext.Setup(x => x.Get()).Returns([server]);
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));

        _sut.EnsureMonitorRunning();
        await Task.Delay(1500);

        handler.RequestCount.Should().BeGreaterThanOrEqualTo(1);
        handler.LastRequestUri.Should().Be("http://127.0.0.1:2303/command");
        _mockProcessUtilities.Verify(x => x.KillProcess(It.IsAny<Process>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task StopServer_WhenShutdownIsUnacknowledged_ResendsWithoutResettingTheStopTimer()
    {
        var requestedAt = DateTime.UtcNow.AddMinutes(-1);
        var server = Server(
            new GameServerStatus
            {
                Running = true,
                CurrentMissionSessionId = "sess-1",
                StopPhase = StopPhase.Ending,
                StopRequestedAt = requestedAt
            }
        );
        _servers.Add(server);
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));

        await _sut.StopServerAsync(server);

        handler.RequestCount.Should().Be(1);
        server.Status.StopRequestedAt.Should().Be(requestedAt);
    }
}
