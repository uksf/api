using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Services;

public class MissionSessionCaptureServiceTests
{
    private readonly Mock<ICampaignMissionsContext> _mockMissions = new();
    private readonly MissionSessionCaptureService _service;

    public MissionSessionCaptureServiceTests()
    {
        _service = new MissionSessionCaptureService(_mockMissions.Object);
    }

    [Fact]
    public async Task CaptureStarted_stamps_most_recent_pending_mission_for_server()
    {
        DomainMission older = new() { Id = "m1", LaunchedServerId = "s1", Status = MissionStatus.Scheduled, LaunchedAt = new DateTime(2026, 6, 1) };
        DomainMission newer = new() { Id = "m2", LaunchedServerId = "s1", Status = MissionStatus.Scheduled, LaunchedAt = new DateTime(2026, 6, 10) };
        _mockMissions.Setup(x => x.Get(It.IsAny<Func<DomainMission, bool>>())).Returns([older, newer]);

        await _service.CaptureStartedAsync("s1", "sess-123");

        _mockMissions.Verify(x => x.Update("m2", It.IsAny<Expression<Func<DomainMission, string>>>(), "sess-123"), Times.Once);
    }

    [Fact]
    public async Task CaptureStarted_noop_when_no_pending_mission()
    {
        _mockMissions.Setup(x => x.Get(It.IsAny<Func<DomainMission, bool>>())).Returns([]);

        await _service.CaptureStartedAsync("s1", "sess-123");

        _mockMissions.Verify(x => x.Update(It.IsAny<string>(), It.IsAny<Expression<Func<DomainMission, string>>>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CaptureEnded_completes_matching_mission()
    {
        DomainMission mission = new() { Id = "m2", SessionId = "sess-123", Status = MissionStatus.Scheduled };
        _mockMissions.Setup(x => x.Get(It.IsAny<Func<DomainMission, bool>>())).Returns([mission]);

        await _service.CaptureEndedAsync("sess-123");

        _mockMissions.Verify(x => x.Update("m2", It.IsAny<Expression<Func<DomainMission, MissionStatus>>>(), MissionStatus.Complete), Times.Once);
    }
}
