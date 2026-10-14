using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core.Processes;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerProcessManagerSessionOrderTests : GameServerProcessManagerTestBase
{
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly DomainGameServer _server;

    public GameServerProcessManagerSessionOrderTests()
    {
        _mockProcessUtilities.Setup(x => x.FindProcessById(_process.Id)).Returns(_process);
        _mockHelpers.Setup(x => x.IsOwnedProcessId(_process.Id)).Returns(true);
        _server = new DomainGameServer
        {
            Id = "s1",
            Name = "Test",
            Port = 2302,
            ApiPort = 2303,
            ProcessId = _process.Id,
            ProcessStartedAt = GameServerProcessManager.CanonicalStartTime(_process.StartTime),
            Status = new GameServerStatus { Running = true, CurrentMissionSessionId = "B" }
        };
        _servers.Add(_server);
        _mockContext.Setup(x => x.Get()).Returns(() => [_server]);
        _mockContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainGameServer, bool>>()))
                    .Returns<Func<DomainGameServer, bool>>(match => _servers.Find(match.Invoke));
    }

    [Fact]
    public async Task GetAllServerStatuses_WhenTheServerLaunchedAfterTheProcessScan_KeepsTracking()
    {
        _mockHelpers.SetupSequence(x => x.GetGameServerArmaProcesses())
                    .Returns([])
                    .Returns([new ProcessCommandLineInfo(_process.Id, "arma3server_x64.exe -config=server.cfg -port=2302")]);

        await _sut.GetAllServerStatusesAsync();

        _server.ProcessId.Should().Be(_process.Id);
        _server.Status.CurrentMissionSessionId.Should().Be("B");
        _mockMissionStatsService.Verify(x => x.FinaliseKilledSessionAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RecordMissionStart_ForASessionThatAlreadyEnded_IsRejectedWithoutSideEffects()
    {
        _mockMissionStatsService.Setup(x => x.GetSessionAsync("A")).ReturnsAsync(new MissionSession { SessionId = "A", MissionEnded = DateTime.UtcNow });

        var accepted = await _sut.RecordMissionSessionAsync(2303, "A", started: true);

        accepted.Should().BeFalse();
        _server.Status.CurrentMissionSessionId.Should().Be("B");
        _mockOpSessionCaptureService.Verify(x => x.CaptureStartedAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RecordMissionStart_ForALiveSession_MakesItCurrentAndCapturesTheCampaignMission()
    {
        var accepted = await _sut.RecordMissionSessionAsync(2303, "C", started: true);

        accepted.Should().BeTrue();
        _server.Status.CurrentMissionSessionId.Should().Be("C");
        _mockOpSessionCaptureService.Verify(x => x.CaptureStartedAsync("s1", "C"), Times.Once);
    }

    [Fact]
    public async Task RecordMissionStart_FromAnOlderProcess_IsRejected()
    {
        var accepted = await _sut.RecordMissionSessionAsync(2303, "C", started: true, _server.ProcessStartedAt!.Value.AddMinutes(-1));

        accepted.Should().BeFalse();
        _server.Status.CurrentMissionSessionId.Should().Be("B");
        _mockOpSessionCaptureService.Verify(x => x.CaptureStartedAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
