using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class OpsControllerTests
{
    private readonly Mock<IOpsContext> _mockContext = new();
    private readonly Mock<IOpsService> _mockOpsService = new();
    private readonly Mock<IGameServerLaunchService> _mockLaunch = new();
    private readonly Mock<IHttpContextService> _mockHttp = new();
    private readonly OpsController _controller;

    public OpsControllerTests()
    {
        _controller = new OpsController(_mockContext.Object, _mockOpsService.Object, _mockLaunch.Object, _mockHttp.Object);
    }

    [Fact]
    public async Task Post_applies_defaults_then_adds()
    {
        DomainOp op = new() { Title = "Alpha" };

        await _controller.Post(op);

        _mockOpsService.Verify(x => x.ApplyDefaults(op), Times.Once);
        _mockContext.Verify(x => x.Add(op), Times.Once);
    }

    [Fact]
    public void GetByCampaign_filters_by_campaignId()
    {
        DomainOp a = new() { CampaignId = "c1", Title = "A" };
        DomainOp b = new() { CampaignId = "c2", Title = "B" };
        _mockContext.Setup(x => x.Get()).Returns([a, b]);
        _mockOpsService.Setup(x => x.ToDto(It.IsAny<DomainOp>())).Returns<DomainOp>(o => new OpDto { Op = o, MissionFileState = MissionFileState.Present });

        var result = _controller.Get("c1").ToList();

        result.Should().HaveCount(1);
        result[0].Op.Should().Be(a);
    }

    [Fact]
    public async Task LaunchOp_blocks_when_mission_file_missing()
    {
        DomainOp op = new() { Id = "op1", ServerId = "s1", MissionName = "gone.Altis.pbo" };
        _mockContext.Setup(x => x.GetSingle("op1")).Returns(op);
        _mockOpsService.Setup(x => x.ToDto(op)).Returns(new OpDto { Op = op, MissionFileState = MissionFileState.Missing });

        var act = () => _controller.LaunchOp("op1");

        await act.Should().ThrowAsync<BadRequestException>();
        _mockLaunch.Verify(x => x.LaunchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LaunchOp_failed_launch_does_not_persist_snapshot()
    {
        DomainOp op = new() { Id = "op1", ServerId = "s1", MissionName = "m.Altis.pbo" };
        _mockContext.Setup(x => x.GetSingle("op1")).Returns(op);
        _mockOpsService.Setup(x => x.ToDto(op)).Returns(new OpDto { Op = op, MissionFileState = MissionFileState.Present });
        _mockHttp.Setup(x => x.GetUserId()).Returns("user1");
        _mockLaunch.Setup(x => x.LaunchAsync("s1", "m.Altis.pbo", "user1")).ThrowsAsync(new BadRequestException("boom"));

        var act = () => _controller.LaunchOp("op1");

        await act.Should().ThrowAsync<BadRequestException>();
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainOp>()), Times.Never);
    }

    [Fact]
    public async Task LaunchOp_snapshots_and_delegates()
    {
        DomainOp op = new() { Id = "op1", ServerId = "s1", MissionName = "m.Altis.pbo" };
        _mockContext.Setup(x => x.GetSingle("op1")).Returns(op);
        _mockOpsService.Setup(x => x.ToDto(op)).Returns(new OpDto { Op = op, MissionFileState = MissionFileState.Present });
        _mockHttp.Setup(x => x.GetUserId()).Returns("user1");
        _mockLaunch.Setup(x => x.LaunchAsync("s1", "m.Altis.pbo", "user1")).ReturnsAsync([]);

        await _controller.LaunchOp("op1");

        _mockContext.Verify(x => x.Replace(It.Is<DomainOp>(o =>
            o.LaunchedServerId == "s1" && o.LaunchedMission == "m.Altis.pbo" && o.LaunchedAt != null)), Times.Once);
        _mockLaunch.Verify(x => x.LaunchAsync("s1", "m.Altis.pbo", "user1"), Times.Once);
    }
}
