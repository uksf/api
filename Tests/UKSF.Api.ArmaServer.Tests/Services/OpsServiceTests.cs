using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class OpsServiceTests
{
    private readonly Mock<IGameServersService> _mockGameServersService = new();
    private readonly Mock<IMissionsService> _mockMissionsService = new();
    private readonly Mock<IOpsContext> _mockOpsContext = new();
    private readonly Mock<IIntelPagesContext> _mockIntelPagesContext = new();
    private readonly OpsService _service;

    public OpsServiceTests()
    {
        _service = new OpsService(_mockGameServersService.Object, _mockMissionsService.Object, _mockOpsContext.Object, _mockIntelPagesContext.Object);
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
    public void ApplyDefaults_falls_back_to_singleton_server_when_no_main_server_named()
    {
        DomainGameServer singleton = new() { Name = "Some Server", ServerOption = GameServerOption.Singleton };
        DomainGameServer other = new() { Name = "Other", ServerOption = GameServerOption.None };
        _mockGameServersService.Setup(x => x.GetServers()).Returns([other, singleton]);
        DomainOp op = new() { Title = "X" };

        _service.ApplyDefaults(op);

        op.ServerId.Should().Be(singleton.Id);
    }

    [Fact]
    public void ApplyDefaults_falls_back_to_first_server_when_no_main_or_singleton()
    {
        DomainGameServer first = new() { Name = "First", ServerOption = GameServerOption.None };
        DomainGameServer second = new() { Name = "Second", ServerOption = GameServerOption.None };
        _mockGameServersService.Setup(x => x.GetServers()).Returns([first, second]);
        DomainOp op = new() { Title = "X" };

        _service.ApplyDefaults(op);

        op.ServerId.Should().Be(first.Id);
    }

    [Fact]
    public void ApplyDefaults_does_not_overwrite_existing_serverId()
    {
        _mockGameServersService.Setup(x => x.GetServers()).Returns([new DomainGameServer { Name = "Main Server" }]);
        DomainOp op = new() { Title = "X", ServerId = "chosen", ScheduledTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };

        _service.ApplyDefaults(op);

        op.ServerId.Should().Be("chosen");
    }

    [Fact]
    public void ApplyDefaults_does_not_overwrite_existing_scheduledTime()
    {
        var chosen = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _mockGameServersService.Setup(x => x.GetServers()).Returns([new DomainGameServer { Name = "Main Server" }]);
        DomainOp op = new() { Title = "X", ServerId = "s1", ScheduledTime = chosen };

        _service.ApplyDefaults(op);

        op.ScheduledTime.Should().Be(chosen);
    }

    [Fact]
    public async Task DeleteOp_deletes_op_scoped_intel_then_the_op()
    {
        Expression<Func<DomainIntelPage, bool>> captured = null;
        _mockIntelPagesContext.Setup(x => x.DeleteMany(It.IsAny<Expression<Func<DomainIntelPage, bool>>>()))
                              .Callback<Expression<Func<DomainIntelPage, bool>>>(e => captured = e)
                              .Returns(Task.CompletedTask);

        await _service.DeleteOp("op1");

        _mockIntelPagesContext.Verify(x => x.DeleteMany(It.IsAny<Expression<Func<DomainIntelPage, bool>>>()), Times.Once);
        _mockOpsContext.Verify(x => x.Delete("op1"), Times.Once);

        var predicate = captured.Compile();
        predicate(new DomainIntelPage { Scope = IntelScope.Op, OwnerId = "op1" }).Should().BeTrue();
        predicate(new DomainIntelPage { Scope = IntelScope.Op, OwnerId = "op2" }).Should().BeFalse();
        predicate(new DomainIntelPage { Scope = IntelScope.Campaign, OwnerId = "op1" }).Should().BeFalse();
    }

    [Fact]
    public void ToDto_reports_missing_mission_file()
    {
        DomainOp op = new() { Title = "X", MissionName = "gone.Altis.pbo" };
        _mockMissionsService.Setup(x => x.FindMissionFilePath("gone.Altis.pbo")).Returns((string)null);

        var dto = _service.ToDto(op);

        dto.MissionFileState.Should().Be(MissionFileState.Missing);
    }

    [Fact]
    public void ToDto_reports_present_when_mission_file_found()
    {
        DomainOp op = new() { Title = "X", MissionName = "here.Altis.pbo" };
        _mockMissionsService.Setup(x => x.FindMissionFilePath("here.Altis.pbo")).Returns("/missions/here.Altis.pbo");

        var dto = _service.ToDto(op);

        dto.Op.Should().Be(op);
        dto.MissionFileState.Should().Be(MissionFileState.Present);
    }

    [Fact]
    public void ToDto_reports_missing_when_missionName_empty()
    {
        DomainOp op = new() { Title = "X", MissionName = "" };

        var dto = _service.ToDto(op);

        dto.MissionFileState.Should().Be(MissionFileState.Missing);
        _mockMissionsService.Verify(x => x.FindMissionFilePath(It.IsAny<string>()), Times.Never);
    }
}
