using System;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class OpsServiceTests
{
    private readonly Mock<IGameServersService> _mockGameServersService = new();
    private readonly Mock<IMissionsService> _mockMissionsService = new();
    private readonly OpsService _service;

    public OpsServiceTests()
    {
        _service = new OpsService(_mockGameServersService.Object, _mockMissionsService.Object);
    }

    [Fact]
    public void NextStandardOpTime_is_next_1900_london_in_utc()
    {
        // 2026-06-10 is a Wednesday; 12:00 UTC. BST (+1) so 19:00 London == 18:00 UTC same day.
        var now = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

        var result = _service.NextStandardOpTimeUtc(now);

        result.Should().Be(new DateTime(2026, 6, 10, 18, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void NextStandardOpTime_rolls_to_tomorrow_when_past_1900()
    {
        // 2026-06-10 20:00 UTC (= 21:00 BST, past 19:00) → next is 2026-06-11 18:00 UTC.
        var now = new DateTime(2026, 6, 10, 20, 0, 0, DateTimeKind.Utc);

        var result = _service.NextStandardOpTimeUtc(now);

        result.Should().Be(new DateTime(2026, 6, 11, 18, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ApplyDefaults_sets_main_server_when_serverId_missing()
    {
        DomainGameServer main = new() { Name = "Main Server" };
        DomainGameServer other = new() { Name = "Other" };
        _mockGameServersService.Setup(x => x.GetServers()).Returns([other, main]);
        DomainOp op = new() { Title = "X" };

        _service.ApplyDefaults(op);

        op.ServerId.Should().Be(main.Id);
        op.ScheduledTime.Should().NotBe(default);
    }

    [Fact]
    public void ToDto_reports_missing_mission_file()
    {
        DomainOp op = new() { Title = "X", MissionName = "gone.Altis.pbo" };
        _mockMissionsService.Setup(x => x.FindMissionFilePath("gone.Altis.pbo")).Returns((string)null);

        var dto = _service.ToDto(op);

        dto.MissionFileState.Should().Be(MissionFileState.Missing);
    }
}
