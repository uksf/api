using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using UKSF.Api.Backups.ScheduledActions;
using UKSF.Api.Backups.Services;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Backups.Tests.ScheduledActions;

public class BackupScheduledActionsTests
{
    private readonly Clock _clock = new();
    private readonly Mock<IBackupRunner> _mockBackupRunner = new();
    private readonly Mock<IBackupWatchdog> _mockBackupWatchdog = new();

    private ActionRunBackup RunAction()
    {
        return new ActionRunBackup(new Mock<ISchedulerService>().Object, new Mock<IHostEnvironment>().Object, _clock, _mockBackupRunner.Object);
    }

    private ActionCheckBackup CheckAction()
    {
        return new ActionCheckBackup(new Mock<ISchedulerService>().Object, new Mock<IHostEnvironment>().Object, _clock, _mockBackupWatchdog.Object);
    }

    [Fact]
    public void The_backup_runs_daily()
    {
        RunAction().RunInterval.Should().Be(TimeSpan.FromDays(1));
        CheckAction().RunInterval.Should().Be(TimeSpan.FromDays(1));
    }

    [Fact]
    public void The_backup_lands_at_0400_uk_in_winter()
    {
        // 2026-01-15 is GMT, so 04:00 UK is 04:00 UTC.
        var next = RunAction().NextRunAfter(new DateTime(2026, 1, 15, 5, 0, 0, DateTimeKind.Utc));

        next.Should().Be(new DateTime(2026, 1, 16, 4, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void The_backup_still_lands_at_0400_uk_in_summer()
    {
        // 2026-08-02 is BST, so 04:00 UK is 03:00 UTC - a fixed UTC schedule would drift by an hour.
        var next = RunAction().NextRunAfter(new DateTime(2026, 8, 2, 5, 0, 0, DateTimeKind.Utc));

        next.Should().Be(new DateTime(2026, 8, 3, 3, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void The_watchdog_runs_two_hours_after_the_backup()
    {
        var next = CheckAction().NextRunAfter(new DateTime(2026, 8, 2, 5, 0, 0, DateTimeKind.Utc));

        next.Should().Be(new DateTime(2026, 8, 3, 5, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Running_the_action_runs_a_backup()
    {
        await RunAction().Run();

        _mockBackupRunner.Verify(x => x.Run(default), Times.Once);
    }

    [Fact]
    public async Task Running_the_check_action_checks_today()
    {
        await CheckAction().Run();

        _mockBackupWatchdog.Verify(x => x.CheckToday(), Times.Once);
    }
}
