using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.ScheduledActions;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.ScheduledActions;

public class ActionLaunchDueMissionsTests
{
    private static readonly DateTime Now = new(2026, 6, 13, 18, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ICampaignMissionsContext> _mockCampaignMissionsContext = new();
    private readonly Mock<ICampaignMissionsService> _mockCampaignMissionsService = new();
    private readonly Mock<IGameServersService> _mockGameServersService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IClock> _mockClock = new();
    private readonly ActionLaunchDueMissions _action;

    public ActionLaunchDueMissionsTests()
    {
        Mock<ISchedulerService> mockSchedulerService = new();
        Mock<IHostEnvironment> mockHostEnvironment = new();
        _mockClock.Setup(x => x.UtcNow()).Returns(Now);
        _mockGameServersService.Setup(x => x.GetServer(It.IsAny<string>())).Returns(new DomainGameServer { Name = "Main Server" });

        _action = new ActionLaunchDueMissions(
            mockSchedulerService.Object,
            mockHostEnvironment.Object,
            _mockClock.Object,
            _mockCampaignMissionsContext.Object,
            _mockCampaignMissionsService.Object,
            _mockGameServersService.Object,
            _mockLogger.Object
        );
    }

    private void SetupMissions(params DomainMission[] missions)
    {
        _mockCampaignMissionsContext.Setup(x => x.Get(It.IsAny<Func<DomainMission, bool>>()))
                                    .Returns<Func<DomainMission, bool>>(predicate => missions.Where(predicate));
    }

    [Fact]
    public void Name_is_ActionLaunchDueMissions()
    {
        _action.Name.Should().Be("ActionLaunchDueMissions");
    }

    [Fact]
    public async Task Run_launches_a_due_mission()
    {
        DomainMission mission = new()
        {
            Id = "m1", Title = "Alpha", ServerId = "s1", MissionName = "m.Altis.pbo",
            Status = MissionStatus.Scheduled, AutoLaunch = true, LaunchedAt = null, ScheduledTime = Now.AddMinutes(-5)
        };
        SetupMissions(mission);
        _mockCampaignMissionsService.Setup(x => x.LaunchMissionAsync(mission, "Scheduler")).ReturnsAsync([]);

        await _action.Run();

        _mockCampaignMissionsService.Verify(x => x.LaunchMissionAsync(mission, "Scheduler"), Times.Once);
        _mockLogger.Verify(x => x.LogAudit(It.Is<string>(s => s.Contains("auto-launched")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Run_skips_mission_not_due_yet()
    {
        DomainMission mission = new()
        {
            Id = "m1", Title = "Alpha", Status = MissionStatus.Scheduled, AutoLaunch = true, LaunchedAt = null, ScheduledTime = Now.AddMinutes(5)
        };
        SetupMissions(mission);

        await _action.Run();

        _mockCampaignMissionsService.Verify(x => x.LaunchMissionAsync(It.IsAny<DomainMission>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Run_skips_already_launched_mission()
    {
        DomainMission mission = new()
        {
            Id = "m1", Title = "Alpha", Status = MissionStatus.Scheduled, AutoLaunch = true,
            LaunchedAt = Now.AddMinutes(-100), ScheduledTime = Now.AddMinutes(-5)
        };
        SetupMissions(mission);

        await _action.Run();

        _mockCampaignMissionsService.Verify(x => x.LaunchMissionAsync(It.IsAny<DomainMission>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Run_skips_mission_without_autolaunch_enabled()
    {
        DomainMission mission = new()
        {
            Id = "m1", Title = "Alpha", Status = MissionStatus.Scheduled, AutoLaunch = false, LaunchedAt = null, ScheduledTime = Now.AddMinutes(-5)
        };
        SetupMissions(mission);

        await _action.Run();

        _mockCampaignMissionsService.Verify(x => x.LaunchMissionAsync(It.IsAny<DomainMission>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Run_skips_mission_beyond_the_grace_window()
    {
        DomainMission mission = new()
        {
            Id = "m1", Title = "Alpha", Status = MissionStatus.Scheduled, AutoLaunch = true, LaunchedAt = null, ScheduledTime = Now.AddMinutes(-45)
        };
        SetupMissions(mission);

        await _action.Run();

        _mockCampaignMissionsService.Verify(x => x.LaunchMissionAsync(It.IsAny<DomainMission>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Run_catches_a_launch_failure_and_continues()
    {
        DomainMission mission = new()
        {
            Id = "m1", Title = "Alpha", ServerId = "s1", MissionName = "m.Altis.pbo",
            Status = MissionStatus.Scheduled, AutoLaunch = true, LaunchedAt = null, ScheduledTime = Now.AddMinutes(-5)
        };
        SetupMissions(mission);
        _mockCampaignMissionsService.Setup(x => x.LaunchMissionAsync(mission, "Scheduler")).ThrowsAsync(new Exception("boom"));

        var act = () => _action.Run();

        await act.Should().NotThrowAsync();
        _mockLogger.Verify(x => x.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
    }
}
