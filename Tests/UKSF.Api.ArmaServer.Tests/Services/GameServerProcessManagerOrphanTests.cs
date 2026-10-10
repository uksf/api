using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Processes;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class GameServerProcessManagerOrphanTests : GameServerProcessManagerTestBase
{
    private const int OrphanPid = 4001;
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly DateTime _now = DateTime.UtcNow;
    private readonly List<DomainGameServer> _servers = [];

    public GameServerProcessManagerOrphanTests()
    {
        _mockContext.Setup(x => x.Get()).Returns(() => _servers);
        _mockProcessUtilities.Setup(x => x.FindProcessById(It.IsAny<int>())).Returns(_process);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(OrphanPid, "")]);
    }

    [Fact]
    public async Task ReconcileOrphans_ReturnsTheOwnedProcessCount()
    {
        var count = await _sut.ReconcileOrphansAsync(_now);

        count.Should().Be(1);
    }

    [Fact]
    public async Task ReconcileOrphans_KillsAnUntrackedProcessAfterFiveMinutes()
    {
        await _sut.ReconcileOrphansAsync(_now);
        await _sut.ReconcileOrphansAsync(_now.AddMinutes(5));

        _mockProcessUtilities.Verify(x => x.KillProcess(_process, It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task ReconcileOrphans_LeavesAnUntrackedProcessAloneBeforeFiveMinutes()
    {
        await _sut.ReconcileOrphansAsync(_now);
        await _sut.ReconcileOrphansAsync(_now.AddMinutes(4));

        _mockProcessUtilities.Verify(x => x.KillProcess(It.IsAny<Process>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileOrphans_NeverKillsATrackedServerProcess()
    {
        _servers.Add(new DomainGameServer { Id = "s1", ProcessId = OrphanPid });

        await _sut.ReconcileOrphansAsync(_now);
        await _sut.ReconcileOrphansAsync(_now.AddMinutes(10));

        _mockProcessUtilities.Verify(x => x.KillProcess(It.IsAny<Process>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileOrphans_NeverKillsATrackedHeadlessClient()
    {
        _servers.Add(
            new DomainGameServer
            {
                Id = "s1",
                ProcessId = 1,
                HeadlessClientProcessIds = [OrphanPid]
            }
        );

        await _sut.ReconcileOrphansAsync(_now);
        await _sut.ReconcileOrphansAsync(_now.AddMinutes(10));

        _mockProcessUtilities.Verify(x => x.KillProcess(It.IsAny<Process>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileOrphans_WhenAServerAdoptsTheProcessAfterItAged_DoesNotKillIt()
    {
        await _sut.ReconcileOrphansAsync(_now);
        _servers.Add(new DomainGameServer { Id = "s1", ProcessId = OrphanPid });

        await _sut.ReconcileOrphansAsync(_now.AddMinutes(10));

        _mockProcessUtilities.Verify(x => x.KillProcess(It.IsAny<Process>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileOrphans_WhenThePidIsReusedByANewProcess_RestartsItsGracePeriod()
    {
        _mockProcessUtilities.Setup(x => x.FindProcessById(OrphanPid)).Returns((Process)null);
        await _sut.ReconcileOrphansAsync(_now);
        _mockProcessUtilities.Setup(x => x.FindProcessById(OrphanPid)).Returns(_process);

        await _sut.ReconcileOrphansAsync(_now.AddMinutes(5));

        _mockProcessUtilities.Verify(x => x.KillProcess(It.IsAny<Process>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileOrphans_WhenTheProcessExits_ForgetsItsAge()
    {
        await _sut.ReconcileOrphansAsync(_now);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([]);
        (await _sut.ReconcileOrphansAsync(_now.AddMinutes(1))).Should().Be(0);
        _mockHelpers.Setup(x => x.GetGameServerArmaProcesses()).Returns([new ProcessCommandLineInfo(OrphanPid, "")]);

        await _sut.ReconcileOrphansAsync(_now.AddMinutes(5));

        _mockProcessUtilities.Verify(x => x.KillProcess(It.IsAny<Process>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileOrphans_WhenTheKillThrows_DoesNotEscape()
    {
        _mockProcessUtilities.Setup(x => x.KillProcess(_process, It.IsAny<bool>())).Throws<System.ComponentModel.Win32Exception>();
        await _sut.ReconcileOrphansAsync(_now);

        var act = () => _sut.ReconcileOrphansAsync(_now.AddMinutes(5));

        await act.Should().NotThrowAsync();
    }
}
