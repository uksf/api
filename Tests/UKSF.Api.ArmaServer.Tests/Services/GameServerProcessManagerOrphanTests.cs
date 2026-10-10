using System;
using System.Diagnostics;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Processes;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerProcessManagerOrphanTests : GameServerProcessManagerTestBase
{
    private static readonly DateTime Start = new(2026, 10, 10, 2, 0, 0, DateTimeKind.Utc);
    private readonly Process _process = Process.GetCurrentProcess();

    private static DomainGameServer Tracked() =>
        new()
        {
            Id = "s1",
            ProcessId = 100,
            HeadlessClientProcessIds = [101],
            Status = new GameServerStatus { StopPhase = StopPhase.Saving }
        };

    private void Running(params int[] processIds)
    {
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns(Array.ConvertAll(processIds, id => new ProcessCommandLineInfo(id, "")));
        foreach (var id in processIds)
        {
            _mockProcessUtilities.Setup(x => x.FindProcessById(id)).Returns(_process);
        }
    }

    [Fact]
    public void KillLongUntrackedProcesses_KillsAnUntrackedProcessOnlyAfterFiveMinutes()
    {
        Running(100, 101, 200);

        _sut.KillLongUntrackedProcesses([Tracked()], Start);
        _sut.KillLongUntrackedProcesses([Tracked()], Start.AddMinutes(4));
        _mockProcessUtilities.Verify(x => x.KillProcess(It.IsAny<Process>(), It.IsAny<bool>()), Times.Never);

        _sut.KillLongUntrackedProcesses([Tracked()], Start.AddMinutes(6));
        _mockProcessUtilities.Verify(x => x.FindProcessById(200), Times.Once);
        _mockProcessUtilities.Verify(x => x.KillProcess(_process, It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public void KillLongUntrackedProcesses_NeverKillsATrackedServerOrItsHeadlessClients()
    {
        Running(100, 101);

        _sut.KillLongUntrackedProcesses([Tracked()], Start);
        _sut.KillLongUntrackedProcesses([Tracked()], Start.AddHours(1));

        _mockProcessUtilities.Verify(x => x.KillProcess(It.IsAny<Process>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void KillLongUntrackedProcesses_ForgetsAProcessThatExitedSoAReusedIdStartsAgain()
    {
        Running(100, 200);
        _sut.KillLongUntrackedProcesses([Tracked()], Start);
        Running(100);
        _sut.KillLongUntrackedProcesses([Tracked()], Start.AddMinutes(3));
        Running(100, 200);
        _sut.KillLongUntrackedProcesses([Tracked()], Start.AddMinutes(6));

        _mockProcessUtilities.Verify(x => x.KillProcess(It.IsAny<Process>(), It.IsAny<bool>()), Times.Never);
    }
}
