using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class OpsControllerTests
{
    private readonly Mock<IOpsContext> _mockContext = new();
    private readonly Mock<IOpsService> _mockOpsService = new();
    private readonly OpsController _controller;

    public OpsControllerTests()
    {
        _controller = new OpsController(_mockContext.Object, _mockOpsService.Object);
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
}
