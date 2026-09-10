using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class IntelPagesControllerTests
{
    private readonly Mock<IIntelPagesContext> _mockContext = new();
    private readonly Mock<ICampaignsContext> _mockCampaigns = new();
    private readonly Mock<IOperationsContext> _mockOperations = new();
    private readonly Mock<ICampaignMissionsContext> _mockMissions = new();
    private readonly Mock<IHttpContextService> _mockHttp = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly IntelPagesController _controller;

    public IntelPagesControllerTests()
    {
        _mockHttp.Setup(x => x.UserHasPermission(Permissions.Command)).Returns(true);
        _mockCampaigns.Setup(x => x.GetSingle("c1")).Returns(new DomainCampaign { Id = "c1", Name = "Op Storm", Status = CampaignStatus.Current });
        _mockOperations.Setup(x => x.GetSingle("op1")).Returns(new DomainOperation { Id = "op1", CampaignId = "c1", Title = "Alpha" });
        _mockMissions.Setup(x => x.GetSingle("m1")).Returns(new DomainMission { Id = "m1", OperationId = "op1", Title = "First" });
        _controller = new IntelPagesController(
            _mockContext.Object,
            _mockCampaigns.Object,
            _mockOperations.Object,
            _mockMissions.Object,
            _mockHttp.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public void Get_filters_by_scope_and_owner()
    {
        DomainIntelPage a = new() { Scope = IntelScope.Operation, OwnerId = "op1", Title = "A" };
        DomainIntelPage b = new() { Scope = IntelScope.Campaign, OwnerId = "c1", Title = "B" };
        _mockContext.Setup(x => x.Get()).Returns([a, b]);

        var result = _controller.Get(IntelScope.Operation, "op1").ToList();

        result.Should().ContainSingle().Which.Should().Be(a);
    }

    [Fact]
    public void Get_throws_when_owner_missing()
    {
        _mockOperations.Setup(x => x.GetSingle("missing")).Returns((DomainOperation)null);

        var act = () => _controller.Get(IntelScope.Operation, "missing");

        act.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void Get_hides_upcoming_campaign_intel_from_non_command()
    {
        _mockHttp.Setup(x => x.UserHasPermission(Permissions.Command)).Returns(false);
        _mockCampaigns.Setup(x => x.GetSingle("up")).Returns(new DomainCampaign { Id = "up", Status = CampaignStatus.Upcoming });

        var act = () => _controller.Get(IntelScope.Campaign, "up");

        act.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void Get_hides_upcoming_campaign_descendants_from_non_command()
    {
        _mockHttp.Setup(x => x.UserHasPermission(Permissions.Command)).Returns(false);
        _mockCampaigns.Setup(x => x.GetSingle("c1")).Returns(new DomainCampaign { Id = "c1", Status = CampaignStatus.Upcoming });

        var act = () => _controller.Get(IntelScope.Mission, "m1");

        act.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void GetById_returns_the_single_page()
    {
        DomainIntelPage a = new() { Id = "i1", Title = "A", Scope = IntelScope.Campaign, OwnerId = "c1" };
        _mockContext.Setup(x => x.GetSingle("i1")).Returns(a);

        var result = _controller.Get("i1");

        result.Should().Be(a);
    }

    [Fact]
    public void GetById_throws_when_page_missing()
    {
        _mockContext.Setup(x => x.GetSingle("missing")).Returns((DomainIntelPage)null);

        var act = () => _controller.Get("missing");

        act.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void GetById_validates_owner_visibility()
    {
        _mockHttp.Setup(x => x.UserHasPermission(Permissions.Command)).Returns(false);
        _mockCampaigns.Setup(x => x.GetSingle("c1")).Returns(new DomainCampaign { Id = "c1", Status = CampaignStatus.Upcoming });
        _mockContext.Setup(x => x.GetSingle("i1")).Returns(new DomainIntelPage { Id = "i1", Scope = IntelScope.Campaign, OwnerId = "c1" });

        var act = () => _controller.Get("i1");

        act.Should().Throw<NotFoundException>();
    }

    [Fact]
    public async Task Post_adds_intel_page_for_valid_owner()
    {
        DomainIntelPage a = new() { Title = "A", Scope = IntelScope.Campaign, OwnerId = "c1" };
        await _controller.Post(a);
        _mockContext.Verify(x => x.Add(a), Times.Once);
    }

    [Fact]
    public async Task Post_rejects_missing_owner()
    {
        _mockCampaigns.Setup(x => x.GetSingle("missing")).Returns((DomainCampaign)null);
        DomainIntelPage a = new() { Title = "A", Scope = IntelScope.Campaign, OwnerId = "missing" };

        var act = () => _controller.Post(a);

        await act.Should().ThrowAsync<BadRequestException>();
        _mockContext.Verify(x => x.Add(It.IsAny<DomainIntelPage>()), Times.Never);
    }

    [Fact]
    public async Task Post_rejects_invalid_scope()
    {
        DomainIntelPage a = new() { Title = "A", Scope = (IntelScope)99, OwnerId = "c1" };

        var act = () => _controller.Post(a);

        await act.Should().ThrowAsync<BadRequestException>();
        _mockContext.Verify(x => x.Add(It.IsAny<DomainIntelPage>()), Times.Never);
    }

    [Fact]
    public async Task Post_rejects_mismatched_scope_owner()
    {
        DomainIntelPage a = new() { Title = "A", Scope = IntelScope.Mission, OwnerId = "c1" };

        var act = () => _controller.Post(a);

        await act.Should().ThrowAsync<BadRequestException>();
        _mockContext.Verify(x => x.Add(It.IsAny<DomainIntelPage>()), Times.Never);
    }

    [Fact]
    public async Task Put_replaces_intel_page_after_validating_stored_and_payload_owners()
    {
        DomainIntelPage stored = new() { Id = "i1", Title = "A", Scope = IntelScope.Campaign, OwnerId = "c1" };
        _mockContext.Setup(x => x.GetSingle("i1")).Returns(stored);
        DomainIntelPage payload = new() { Id = "i1", Title = "B", Scope = IntelScope.Operation, OwnerId = "op1" };

        await _controller.Put(payload);

        _mockContext.Verify(x => x.Replace(payload), Times.Once);
    }

    [Fact]
    public async Task Put_does_not_bypass_owner_check_by_changing_payload()
    {
        _mockContext.Setup(x => x.GetSingle("i1")).Returns(new DomainIntelPage { Id = "i1", Scope = IntelScope.Campaign, OwnerId = "c1" });
        DomainIntelPage payload = new() { Id = "i1", Title = "B", Scope = IntelScope.Mission, OwnerId = "missing" };

        var act = () => _controller.Put(payload);

        await act.Should().ThrowAsync<BadRequestException>();
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainIntelPage>()), Times.Never);
    }

    [Fact]
    public async Task Delete_removes_intel_page()
    {
        _mockContext.Setup(x => x.GetSingle("i1")).Returns(new DomainIntelPage { Id = "i1", Title = "A", Scope = IntelScope.Campaign, OwnerId = "c1" });

        await _controller.Delete("i1");
        _mockContext.Verify(x => x.Delete("i1"), Times.Once);
    }
}
