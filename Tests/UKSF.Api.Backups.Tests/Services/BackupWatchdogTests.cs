using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.Backups.DataContext;
using UKSF.Api.Backups.Models;
using UKSF.Api.Backups.Services;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Backups.Tests.Services;

public class BackupWatchdogTests
{
    private static readonly DateTime Now = new(2026, 8, 2, 6, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IBackupAlertService> _mockAlertService = new();
    private readonly Mock<IBackupRunsContext> _mockRunsContext = new();
    private readonly BackupWatchdog _subject;
    private List<DomainBackupRun> _runs = [];

    public BackupWatchdogTests()
    {
        var mockClock = new Mock<IClock>();
        mockClock.Setup(x => x.UtcNow()).Returns(Now);

        _mockRunsContext.Setup(x => x.Get(It.IsAny<Func<DomainBackupRun, bool>>())).Returns((Func<DomainBackupRun, bool> predicate) => _runs.Where(predicate));

        _subject = new BackupWatchdog(_mockRunsContext.Object, _mockAlertService.Object, mockClock.Object);
    }

    private void GivenRuns(params DomainBackupRun[] runs)
    {
        _runs = runs.ToList();
    }

    [Fact]
    public async Task A_success_today_raises_nothing()
    {
        GivenRuns(new DomainBackupRun { Started = Now.Date.AddHours(4), State = BackupRunState.Success });

        await _subject.CheckToday();

        _mockAlertService.Verify(x => x.Alert(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task A_failed_run_today_still_alerts_because_no_backup_was_produced()
    {
        GivenRuns(
            new DomainBackupRun { Started = Now.Date.AddHours(4), State = BackupRunState.Failed },
            new DomainBackupRun { Started = Now.Date.AddDays(-1).AddHours(4), State = BackupRunState.Success }
        );

        await _subject.CheckToday();

        _mockAlertService.Verify(x => x.Alert(It.Is<string>(y => y.Contains("no successful backup today"))), Times.Once);
    }

    [Fact]
    public async Task A_night_where_nothing_ran_at_all_alerts()
    {
        GivenRuns(new DomainBackupRun { Started = Now.Date.AddDays(-1).AddHours(4), State = BackupRunState.Success });

        await _subject.CheckToday();

        _mockAlertService.Verify(x => x.Alert(It.Is<string>(y => y.Contains("last success since"))), Times.Once);
    }

    [Fact]
    public async Task Never_having_succeeded_alerts()
    {
        GivenRuns();

        await _subject.CheckToday();

        _mockAlertService.Verify(x => x.Alert(It.Is<string>(y => y.Contains("last success ever"))), Times.Once);
    }

    [Fact]
    public async Task A_recent_success_passes_the_startup_check()
    {
        GivenRuns(new DomainBackupRun { Started = Now.AddHours(-25), State = BackupRunState.Success });

        await _subject.CheckOnStartup();

        _mockAlertService.Verify(x => x.Alert(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task A_success_older_than_the_tolerance_alerts_on_startup()
    {
        GivenRuns(new DomainBackupRun { Started = Now.AddHours(-27), State = BackupRunState.Success });

        await _subject.CheckOnStartup();

        _mockAlertService.Verify(x => x.Alert(It.Is<string>(y => y.Contains("overdue on startup"))), Times.Once);
    }

    [Fact]
    public async Task No_history_at_all_alerts_on_startup()
    {
        GivenRuns();

        await _subject.CheckOnStartup();

        _mockAlertService.Verify(x => x.Alert(It.Is<string>(y => y.Contains("no backup has ever succeeded"))), Times.Once);
    }

    [Fact]
    public async Task The_newest_success_is_the_one_that_counts()
    {
        GivenRuns(
            new DomainBackupRun { Started = Now.AddDays(-5), State = BackupRunState.Success },
            new DomainBackupRun { Started = Now.Date.AddHours(4), State = BackupRunState.Success },
            new DomainBackupRun { Started = Now.AddDays(-2), State = BackupRunState.Success }
        );

        var last = await _subject.LastSuccess();

        last.Started.Should().Be(Now.Date.AddHours(4));
    }
}
