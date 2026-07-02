using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class CampaignsControllerTests
{
    private readonly Mock<ICampaignsContext> _mockContext = new();
    private readonly Mock<IOpsContext> _mockOpsContext = new();
    private readonly Mock<IIntelPagesContext> _mockIntelPagesContext = new();
    private readonly Mock<IOpsService> _mockOpsService = new();
    private readonly Mock<IHttpContextService> _mockHttpContextService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly CampaignsController _controller;

    public CampaignsControllerTests()
    {
        _mockOpsContext.Setup(x => x.Get(It.IsAny<Func<DomainOp, bool>>())).Returns([]);
        _mockHttpContextService.Setup(x => x.UserHasPermission(Permissions.Command)).Returns(true);
        _mockContext.Setup(x => x.GetSingle(It.IsAny<string>())).Returns(new DomainCampaign { Name = "Op Storm" });
        _controller = new CampaignsController(
            _mockContext.Object,
            _mockOpsContext.Object,
            _mockIntelPagesContext.Object,
            _mockOpsService.Object,
            _mockHttpContextService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public void Get_returns_all_campaigns()
    {
        DomainCampaign a = new() { Name = "Op Storm" };
        _mockContext.Setup(x => x.Get()).Returns([a]);

        var result = _controller.Get();

        result.Should().Contain(a);
    }

    [Fact]
    public void Get_hides_upcoming_campaigns_from_non_command()
    {
        _mockHttpContextService.Setup(x => x.UserHasPermission(Permissions.Command)).Returns(false);
        DomainCampaign current = new() { Name = "Current", Status = CampaignStatus.Current };
        DomainCampaign upcoming = new() { Name = "Upcoming", Status = CampaignStatus.Upcoming };
        _mockContext.Setup(x => x.Get()).Returns([current, upcoming]);

        var result = _controller.Get().ToList();

        result.Should().ContainSingle().Which.Should().Be(current);
    }

    [Fact]
    public void Get_includes_upcoming_campaigns_for_command()
    {
        DomainCampaign current = new() { Name = "Current", Status = CampaignStatus.Current };
        DomainCampaign upcoming = new() { Name = "Upcoming", Status = CampaignStatus.Upcoming };
        _mockContext.Setup(x => x.Get()).Returns([current, upcoming]);

        var result = _controller.Get().ToList();

        result.Should().HaveCount(2);
    }

    [Fact]
    public void GetSingle_throws_when_non_command_requests_upcoming()
    {
        _mockHttpContextService.Setup(x => x.UserHasPermission(Permissions.Command)).Returns(false);
        _mockContext.Setup(x => x.GetSingle("up")).Returns(new DomainCampaign { Name = "Upcoming", Status = CampaignStatus.Upcoming });

        var act = () => _controller.Get("up");

        act.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void GetSingle_returns_upcoming_campaign_for_command()
    {
        DomainCampaign upcoming = new() { Name = "Upcoming", Status = CampaignStatus.Upcoming };
        _mockContext.Setup(x => x.GetSingle("up")).Returns(upcoming);

        var result = _controller.Get("up");

        result.Should().Be(upcoming);
    }

    [Fact]
    public void GetSingle_returns_current_campaign_for_non_command()
    {
        _mockHttpContextService.Setup(x => x.UserHasPermission(Permissions.Command)).Returns(false);
        DomainCampaign current = new() { Name = "Current", Status = CampaignStatus.Current };
        _mockContext.Setup(x => x.GetSingle("cur")).Returns(current);

        var result = _controller.Get("cur");

        result.Should().Be(current);
    }

    [Fact]
    public async Task Post_adds_a_campaign()
    {
        DomainCampaign a = new() { Name = "Op Storm" };

        await _controller.Post(a);

        _mockContext.Verify(x => x.Add(a), Times.Once);
    }

    [Fact]
    public async Task Put_replaces_a_campaign()
    {
        DomainCampaign a = new() { Id = "c1", Name = "Op Storm" };

        await _controller.Put(a);

        _mockContext.Verify(x => x.Replace(a), Times.Once);
    }

    [Fact]
    public async Task Delete_removes_a_campaign()
    {
        await _controller.Delete("abc");

        _mockContext.Verify(x => x.Delete("abc"), Times.Once);
    }

    [Fact]
    public async Task Delete_cascades_child_ops_and_campaign_intel()
    {
        DomainOp op1 = new() { Id = "o1", CampaignId = "c1" };
        DomainOp op2 = new() { Id = "o2", CampaignId = "c1" };
        DomainOp other = new() { Id = "o3", CampaignId = "c2" };
        _mockOpsContext.Setup(x => x.Get(It.IsAny<Func<DomainOp, bool>>()))
                       .Returns<Func<DomainOp, bool>>(predicate => new[] { op1, op2, other }.Where(predicate));

        Expression<Func<DomainIntelPage, bool>> captured = null;
        _mockIntelPagesContext.Setup(x => x.DeleteMany(It.IsAny<Expression<Func<DomainIntelPage, bool>>>()))
                              .Callback<Expression<Func<DomainIntelPage, bool>>>(e => captured = e)
                              .Returns(Task.CompletedTask);

        await _controller.Delete("c1");

        _mockOpsService.Verify(x => x.DeleteOp("o1"), Times.Once);
        _mockOpsService.Verify(x => x.DeleteOp("o2"), Times.Once);
        _mockOpsService.Verify(x => x.DeleteOp("o3"), Times.Never);
        _mockIntelPagesContext.Verify(x => x.DeleteMany(It.IsAny<Expression<Func<DomainIntelPage, bool>>>()), Times.Once);
        _mockContext.Verify(x => x.Delete("c1"), Times.Once);

        var predicate = captured.Compile();
        predicate(new DomainIntelPage { Scope = IntelScope.Campaign, OwnerId = "c1" }).Should().BeTrue();
        predicate(new DomainIntelPage { Scope = IntelScope.Campaign, OwnerId = "c2" }).Should().BeFalse();
        predicate(new DomainIntelPage { Scope = IntelScope.Op, OwnerId = "c1" }).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_rejects_past_campaign()
    {
        _mockContext.Setup(x => x.GetSingle("past")).Returns(new DomainCampaign { Id = "past", Status = CampaignStatus.Past });

        var act = () => _controller.Delete("past");

        await act.Should().ThrowAsync<BadRequestException>();
        _mockOpsService.Verify(x => x.DeleteOp(It.IsAny<string>()), Times.Never);
        _mockIntelPagesContext.Verify(x => x.DeleteMany(It.IsAny<Expression<Func<DomainIntelPage, bool>>>()), Times.Never);
        _mockContext.Verify(x => x.Delete(It.IsAny<string>()), Times.Never);
    }
}
