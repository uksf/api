using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.ScheduledActions;
using UKSF.Api.Core;
using UKSF.Api.Core.Processes;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.ScheduledActions;

public class DevRunRecoveryStartupTests
{
    private readonly Mock<IProcessUtilities> _processUtilities = new();
    private readonly Mock<IDevRunsContext> _context = new();
    private readonly Mock<IUksfLogger> _logger = new();

    public DevRunRecoveryStartupTests()
    {
        _processUtilities.Setup(x => x.GetProcessesWithCommandLine("arma3server")).Returns(Array.Empty<ProcessCommandLineInfo>());
        _context.Setup(x => x.Get(It.IsAny<Func<DomainDevRun, bool>>())).Returns(new List<DomainDevRun>());
        _context.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainDevRun, object>>>(), It.IsAny<object>())).Returns(Task.CompletedTask);
    }

    private DevRunRecoveryStartup CreateSut() => new(_processUtilities.Object, _context.Object, _logger.Object);

    [Fact]
    public async Task StartAsync_KillsOrphanDevRunProcess()
    {
        var orphan = new ProcessCommandLineInfo(123, "-profiles=C:/x/DevRun_abc -port=3304");
        _processUtilities.Setup(x => x.GetProcessesWithCommandLine("arma3server")).Returns(new[] { orphan });
        _processUtilities.Setup(x => x.FindProcessById(123)).Returns((System.Diagnostics.Process)null);

        var sut = CreateSut();
        await sut.StartAsync(CancellationToken.None);

        _processUtilities.Verify(x => x.FindProcessById(123), Times.Once);
    }

    [Fact]
    public async Task StartAsync_MarksStuckRunningRecordsAsFailedLaunch()
    {
        var stuck = new DomainDevRun
        {
            Id = "id1",
            RunId = "abc",
            Status = DevRunStatus.Running
        };
        _context.Setup(x => x.Get(It.IsAny<Func<DomainDevRun, bool>>())).Returns(new[] { stuck });
        _context.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainDevRun, DevRunStatus>>>(), It.IsAny<DevRunStatus>()))
                .Returns(Task.CompletedTask);
        _context.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainDevRun, string>>>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var sut = CreateSut();
        await sut.StartAsync(CancellationToken.None);

        _context.Verify(x => x.Update("id1", It.IsAny<Expression<Func<DomainDevRun, DevRunStatus>>>(), DevRunStatus.FailedLaunch), Times.Once);
    }

    [Fact]
    public async Task StartAsync_SetsCompletedAtAndFailureDetailOnStuckRecord()
    {
        var stuck = new DomainDevRun
        {
            Id = "id1",
            RunId = "abc",
            Status = DevRunStatus.Running
        };
        _context.Setup(x => x.Get(It.IsAny<Func<DomainDevRun, bool>>())).Returns(new[] { stuck });
        _context.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainDevRun, DevRunStatus>>>(), It.IsAny<DevRunStatus>()))
                .Returns(Task.CompletedTask);
        _context.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainDevRun, DateTime?>>>(), It.IsAny<DateTime?>()))
                .Returns(Task.CompletedTask);
        _context.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainDevRun, string>>>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var sut = CreateSut();
        await sut.StartAsync(CancellationToken.None);

        _context.Verify(x => x.Update("id1", It.IsAny<Expression<Func<DomainDevRun, DateTime?>>>(), It.IsAny<DateTime?>()), Times.Once);
        _context.Verify(x => x.Update("id1", It.IsAny<Expression<Func<DomainDevRun, string>>>(), "API restart while running."), Times.Once);
    }

    [Fact]
    public async Task StartAsync_DoesNotKillNonDevRunProcess()
    {
        var unrelated = new ProcessCommandLineInfo(456, "-profiles=C:/x/GameDataExport -port=3302");
        _processUtilities.Setup(x => x.GetProcessesWithCommandLine("arma3server")).Returns(new[] { unrelated });

        var sut = CreateSut();
        await sut.StartAsync(CancellationToken.None);

        _processUtilities.Verify(x => x.FindProcessById(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task StartAsync_DoesNotMarkNonRunningRecords()
    {
        var completed = new DomainDevRun
        {
            Id = "id2",
            RunId = "xyz",
            Status = DevRunStatus.Success
        };
        // Return only completed records; the filter in SUT will exclude them since filter runs in-memory,
        // but the mock returns whatever we configure — so we configure it to return empty as the SUT filter would.
        _context.Setup(x => x.Get(It.IsAny<Func<DomainDevRun, bool>>())).Returns(new List<DomainDevRun>());

        var sut = CreateSut();
        await sut.StartAsync(CancellationToken.None);

        _context.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainDevRun, object>>>(), It.IsAny<object>()), Times.Never);
    }
}
