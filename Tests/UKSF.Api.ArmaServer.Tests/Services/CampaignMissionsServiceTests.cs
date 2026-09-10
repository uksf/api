using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class CampaignMissionsServiceTests
{
    private readonly Mock<IGameServersService> _mockGameServersService = new();
    private readonly Mock<IMissionsService> _mockMissionsService = new();
    private readonly Mock<ICampaignMissionsContext> _mockCampaignMissionsContext = new();
    private readonly Mock<IIntelPagesContext> _mockIntelPagesContext = new();
    private readonly Mock<IGameServerLaunchService> _mockGameServerLaunchService = new();
    private readonly CampaignMissionsService _service;

    public CampaignMissionsServiceTests()
    {
        _service = new CampaignMissionsService(
            _mockGameServersService.Object,
            _mockMissionsService.Object,
            _mockCampaignMissionsContext.Object,
            _mockIntelPagesContext.Object,
            _mockGameServerLaunchService.Object
        );
    }

    [Fact]
    public void NextStandardOpTime_advances_to_upcoming_saturday_from_a_weekday()
    {
        // 2026-06-10 is a Wednesday; 12:00 UTC = 13:00 BST (before 19:00). Next Saturday is 2026-06-13.
        var now = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

        var result = _service.NextStandardOpTimeUtc(now);

        result.Should().Be(new DateTime(2026, 6, 13, 18, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void NextStandardOpTime_advances_to_upcoming_saturday_even_when_time_already_past()
    {
        // 2026-06-10 20:00 UTC (= 21:00 BST, past 19:00) is still a Wednesday. Target is still 2026-06-13.
        var now = new DateTime(2026, 6, 10, 20, 0, 0, DateTimeKind.Utc);

        var result = _service.NextStandardOpTimeUtc(now);

        result.Should().Be(new DateTime(2026, 6, 13, 18, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void NextStandardOpTime_uses_today_when_today_is_saturday_before_1900()
    {
        // 2026-06-13 is a Saturday. 12:00 UTC = 13:00 BST (before 19:00) → same day 19:00 BST.
        var now = new DateTime(2026, 6, 13, 12, 0, 0, DateTimeKind.Utc);

        var result = _service.NextStandardOpTimeUtc(now);

        result.Should().Be(new DateTime(2026, 6, 13, 18, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void NextStandardOpTime_rolls_to_next_saturday_when_today_is_saturday_after_1900()
    {
        // 2026-06-13 is a Saturday. 20:00 UTC = 21:00 BST (past 19:00) → next Saturday, 2026-06-20.
        var now = new DateTime(2026, 6, 13, 20, 0, 0, DateTimeKind.Utc);

        var result = _service.NextStandardOpTimeUtc(now);

        result.Should().Be(new DateTime(2026, 6, 20, 18, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void NextStandardOpTime_stores_1900_london_as_utc_in_gmt()
    {
        // 2026-01-10 is a Saturday in GMT. 12:00 UTC is before 19:00 local → same day 19:00 UTC.
        var now = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);

        var result = _service.NextStandardOpTimeUtc(now);

        result.Should().Be(new DateTime(2026, 1, 10, 19, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ApplyDefaults_sets_main_server_when_serverId_missing()
    {
        DomainGameServer main = new() { Name = "Main Server" };
        DomainGameServer other = new() { Name = "Other" };
        _mockGameServersService.Setup(x => x.GetServers()).Returns([other, main]);
        DomainMission mission = new() { Title = "X" };

        _service.ApplyDefaults(mission);

        mission.ServerId.Should().Be(main.Id);
        mission.ScheduledTime.Should().NotBe(default);
    }

    [Fact]
    public void ApplyDefaults_falls_back_to_singleton_server_when_no_main_server_named()
    {
        DomainGameServer singleton = new() { Name = "Some Server", ServerOption = GameServerOption.Singleton };
        DomainGameServer other = new() { Name = "Other", ServerOption = GameServerOption.None };
        _mockGameServersService.Setup(x => x.GetServers()).Returns([other, singleton]);
        DomainMission mission = new() { Title = "X" };

        _service.ApplyDefaults(mission);

        mission.ServerId.Should().Be(singleton.Id);
    }

    [Fact]
    public void ApplyDefaults_falls_back_to_first_server_when_no_main_or_singleton()
    {
        DomainGameServer first = new() { Name = "First", ServerOption = GameServerOption.None };
        DomainGameServer second = new() { Name = "Second", ServerOption = GameServerOption.None };
        _mockGameServersService.Setup(x => x.GetServers()).Returns([first, second]);
        DomainMission mission = new() { Title = "X" };

        _service.ApplyDefaults(mission);

        mission.ServerId.Should().Be(first.Id);
    }

    [Fact]
    public void ApplyDefaults_does_not_overwrite_existing_serverId()
    {
        _mockGameServersService.Setup(x => x.GetServers()).Returns([new DomainGameServer { Name = "Main Server" }]);
        DomainMission mission = new() { Title = "X", ServerId = "chosen", ScheduledTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };

        _service.ApplyDefaults(mission);

        mission.ServerId.Should().Be("chosen");
    }

    [Fact]
    public void ApplyDefaults_does_not_overwrite_existing_scheduledTime()
    {
        var chosen = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _mockGameServersService.Setup(x => x.GetServers()).Returns([new DomainGameServer { Name = "Main Server" }]);
        DomainMission mission = new() { Title = "X", ServerId = "s1", ScheduledTime = chosen };

        _service.ApplyDefaults(mission);

        mission.ScheduledTime.Should().Be(chosen);
    }

    [Fact]
    public async Task DeleteMission_deletes_mission_scoped_intel_then_the_mission()
    {
        Expression<Func<DomainIntelPage, bool>> captured = null;
        _mockIntelPagesContext.Setup(x => x.DeleteMany(It.IsAny<Expression<Func<DomainIntelPage, bool>>>()))
                              .Callback<Expression<Func<DomainIntelPage, bool>>>(e => captured = e)
                              .Returns(Task.CompletedTask);

        await _service.DeleteMission("m1");

        _mockIntelPagesContext.Verify(x => x.DeleteMany(It.IsAny<Expression<Func<DomainIntelPage, bool>>>()), Times.Once);
        _mockCampaignMissionsContext.Verify(x => x.Delete("m1"), Times.Once);

        var predicate = captured.Compile();
        predicate(new DomainIntelPage { Scope = IntelScope.Mission, OwnerId = "m1" }).Should().BeTrue();
        predicate(new DomainIntelPage { Scope = IntelScope.Mission, OwnerId = "m2" }).Should().BeFalse();
        predicate(new DomainIntelPage { Scope = IntelScope.Operation, OwnerId = "m1" }).Should().BeFalse();
        predicate(new DomainIntelPage { Scope = IntelScope.Campaign, OwnerId = "m1" }).Should().BeFalse();
    }

    [Fact]
    public void ToDto_reports_missing_mission_file()
    {
        DomainMission mission = new() { Title = "X", MissionName = "gone.Altis.pbo" };
        _mockMissionsService.Setup(x => x.FindMissionFilePath("gone.Altis.pbo")).Returns((string)null);

        var dto = _service.ToDto(mission);

        dto.MissionFileState.Should().Be(MissionFileState.Missing);
    }

    [Fact]
    public void ToDto_reports_present_when_mission_file_found()
    {
        DomainMission mission = new() { Title = "X", MissionName = "here.Altis.pbo" };
        _mockMissionsService.Setup(x => x.FindMissionFilePath("here.Altis.pbo")).Returns("/missions/here.Altis.pbo");

        var dto = _service.ToDto(mission);

        dto.Mission.Should().Be(mission);
        dto.MissionFileState.Should().Be(MissionFileState.Present);
    }

    [Fact]
    public void ToDto_reports_missing_when_missionName_empty()
    {
        DomainMission mission = new() { Title = "X", MissionName = "" };

        var dto = _service.ToDto(mission);

        dto.MissionFileState.Should().Be(MissionFileState.Missing);
        _mockMissionsService.Verify(x => x.FindMissionFilePath(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LaunchMissionAsync_throws_when_mission_file_missing()
    {
        DomainMission mission = new() { Id = "m1", MissionName = "gone.Altis.pbo", ServerId = "s1" };
        _mockMissionsService.Setup(x => x.FindMissionFilePath("gone.Altis.pbo")).Returns((string)null);

        var act = () => _service.LaunchMissionAsync(mission, "user1");

        await act.Should().ThrowAsync<BadRequestException>();
        _mockGameServerLaunchService.Verify(x => x.LaunchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LaunchMissionAsync_does_not_persist_when_launch_fails()
    {
        DomainMission mission = new() { Id = "m1", MissionName = "m.Altis.pbo", ServerId = "s1" };
        _mockMissionsService.Setup(x => x.FindMissionFilePath("m.Altis.pbo")).Returns("/missions/m.Altis.pbo");
        _mockGameServerLaunchService.Setup(x => x.LaunchAsync("s1", "m.Altis.pbo", "user1")).ThrowsAsync(new BadRequestException("boom"));

        var act = () => _service.LaunchMissionAsync(mission, "user1");

        await act.Should().ThrowAsync<BadRequestException>();
        _mockCampaignMissionsContext.Verify(x => x.Replace(It.IsAny<DomainMission>()), Times.Never);
    }

    [Fact]
    public async Task LaunchMissionAsync_snapshots_and_resets_session_and_status_on_success()
    {
        DomainMission mission = new()
        {
            Id = "m1", MissionName = "m.Altis.pbo", ServerId = "s1", Status = MissionStatus.Complete, SessionId = "stale-session"
        };
        _mockMissionsService.Setup(x => x.FindMissionFilePath("m.Altis.pbo")).Returns("/missions/m.Altis.pbo");
        _mockGameServerLaunchService.Setup(x => x.LaunchAsync("s1", "m.Altis.pbo", "user1")).ReturnsAsync([]);

        await _service.LaunchMissionAsync(mission, "user1");

        _mockCampaignMissionsContext.Verify(x => x.Replace(It.Is<DomainMission>(o =>
            o.LaunchedServerId == "s1" && o.LaunchedMission == "m.Altis.pbo" && o.LaunchedAt != null
            && o.SessionId == null && o.Status == MissionStatus.Scheduled)), Times.Once);
    }
}
