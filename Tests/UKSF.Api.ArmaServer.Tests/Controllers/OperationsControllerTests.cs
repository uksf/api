using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
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

public class OperationsControllerTests
{
    private readonly Mock<IOperationsContext> _mockContext = new();
    private readonly Mock<ICampaignsContext> _mockCampaigns = new();
    private readonly Mock<IOperationsService> _mockOperationsService = new();
    private readonly Mock<IHttpContextService> _mockHttp = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly OperationsController _controller;

    public OperationsControllerTests()
    {
        _mockHttp.Setup(x => x.UserHasPermission(Permissions.Command)).Returns(true);
        _mockCampaigns.Setup(x => x.GetSingle("c1")).Returns(new DomainCampaign { Id = "c1", Name = "Op Storm", Status = CampaignStatus.Current });
        _controller = new OperationsController(
            _mockContext.Object,
            _mockCampaigns.Object,
            _mockOperationsService.Object,
            _mockHttp.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public void Route_is_nested_under_campaign()
    {
        typeof(OperationsController).GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("campaigns/{campaignId}/operations");
    }

    [Fact]
    public void Controller_requires_member_permission()
    {
        var attribute = typeof(OperationsController).GetCustomAttributes(typeof(PermissionsAttribute), inherit: false).Cast<PermissionsAttribute>().Single();
        attribute.Roles.Should().Be(Permissions.Member);
    }

    [Fact]
    public void Mutations_require_command_permission()
    {
        RolesOn(nameof(OperationsController.Post)).Should().Be(Permissions.Command);
        RolesOn(nameof(OperationsController.Put)).Should().Be(Permissions.Command);
        RolesOn(nameof(OperationsController.Delete)).Should().Be(Permissions.Command);
    }

    [Fact]
    public void Get_returns_operations_for_campaign()
    {
        DomainOperation a = new() { Id = "op1", CampaignId = "c1", Title = "A" };
        DomainOperation b = new() { Id = "op2", CampaignId = "c2", Title = "B" };
        _mockContext.Setup(x => x.Get(It.IsAny<Func<DomainOperation, bool>>()))
                    .Returns<Func<DomainOperation, bool>>(predicate => new[] { a, b }.Where(predicate));

        var result = _controller.Get("c1").ToList();

        result.Should().ContainSingle().Which.Should().Be(a);
    }

    [Fact]
    public void Get_throws_when_campaign_missing()
    {
        _mockCampaigns.Setup(x => x.GetSingle("missing")).Returns((DomainCampaign)null);

        var act = () => _controller.Get("missing");

        act.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void Get_hides_upcoming_campaign_from_non_command()
    {
        _mockHttp.Setup(x => x.UserHasPermission(Permissions.Command)).Returns(false);
        _mockCampaigns.Setup(x => x.GetSingle("up")).Returns(new DomainCampaign { Id = "up", Status = CampaignStatus.Upcoming });

        var act = () => _controller.Get("up");

        act.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void GetById_returns_operation_when_ancestors_match()
    {
        DomainOperation operation = new() { Id = "op1", CampaignId = "c1", Title = "A" };
        _mockContext.Setup(x => x.GetSingle("op1")).Returns(operation);

        var result = _controller.GetById("c1", "op1");

        result.Should().Be(operation);
    }

    [Fact]
    public void GetById_throws_when_operation_belongs_to_another_campaign()
    {
        _mockContext.Setup(x => x.GetSingle("op1")).Returns(new DomainOperation { Id = "op1", CampaignId = "c2" });

        var act = () => _controller.GetById("c1", "op1");

        act.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void GetById_throws_when_operation_missing()
    {
        _mockContext.Setup(x => x.GetSingle("missing")).Returns((DomainOperation)null);

        var act = () => _controller.GetById("c1", "missing");

        act.Should().Throw<NotFoundException>();
    }

    [Fact]
    public async Task Post_sets_route_campaign_id_and_adds()
    {
        DomainOperation operation = new() { Title = "Alpha" };

        await _controller.Post("c1", operation);

        operation.CampaignId.Should().Be("c1");
        _mockContext.Verify(x => x.Add(operation), Times.Once);
    }

    [Fact]
    public async Task Post_rejects_reparenting_payload()
    {
        DomainOperation operation = new() { Title = "Alpha", CampaignId = "c2" };

        var act = () => _controller.Post("c1", operation);

        await act.Should().ThrowAsync<BadRequestException>();
        _mockContext.Verify(x => x.Add(It.IsAny<DomainOperation>()), Times.Never);
    }

    [Fact]
    public async Task Put_replaces_when_ids_match()
    {
        DomainOperation stored = new() { Id = "op1", CampaignId = "c1", Title = "A", Status = OperationStatus.Current };
        _mockContext.Setup(x => x.GetSingle("op1")).Returns(stored);
        DomainOperation payload = new() { Id = "op1", CampaignId = "c1", Title = "Updated", Status = OperationStatus.Past };

        await _controller.Put("c1", "op1", payload);

        _mockContext.Verify(x => x.Replace(It.Is<DomainOperation>(o => o.Id == "op1" && o.CampaignId == "c1" && o.Title == "Updated")), Times.Once);
    }

    [Fact]
    public async Task Put_rejects_body_id_mismatch()
    {
        _mockContext.Setup(x => x.GetSingle("op1")).Returns(new DomainOperation { Id = "op1", CampaignId = "c1" });
        DomainOperation payload = new() { Id = "op2", CampaignId = "c1", Title = "A" };

        var act = () => _controller.Put("c1", "op1", payload);

        await act.Should().ThrowAsync<BadRequestException>();
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainOperation>()), Times.Never);
    }

    [Fact]
    public async Task Put_rejects_reparenting_payload()
    {
        _mockContext.Setup(x => x.GetSingle("op1")).Returns(new DomainOperation { Id = "op1", CampaignId = "c1" });
        DomainOperation payload = new() { Id = "op1", CampaignId = "c2", Title = "A" };

        var act = () => _controller.Put("c1", "op1", payload);

        await act.Should().ThrowAsync<BadRequestException>();
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainOperation>()), Times.Never);
    }

    [Fact]
    public async Task Put_throws_when_stored_operation_has_wrong_campaign()
    {
        _mockContext.Setup(x => x.GetSingle("op1")).Returns(new DomainOperation { Id = "op1", CampaignId = "c2" });
        DomainOperation payload = new() { Id = "op1", CampaignId = "c1", Title = "A" };

        var act = () => _controller.Put("c1", "op1", payload);

        await act.Should().ThrowAsync<NotFoundException>();
        _mockContext.Verify(x => x.Replace(It.IsAny<DomainOperation>()), Times.Never);
    }

    [Fact]
    public async Task Delete_delegates_to_operations_service_cascade()
    {
        _mockContext.Setup(x => x.GetSingle("op1")).Returns(new DomainOperation { Id = "op1", Title = "A", CampaignId = "c1" });

        await _controller.Delete("c1", "op1");

        _mockOperationsService.Verify(x => x.DeleteOperation("op1"), Times.Once);
    }

    [Fact]
    public async Task Delete_throws_when_operation_missing()
    {
        _mockContext.Setup(x => x.GetSingle("missing")).Returns((DomainOperation)null);

        var act = () => _controller.Delete("c1", "missing");

        await act.Should().ThrowAsync<NotFoundException>();
        _mockOperationsService.Verify(x => x.DeleteOperation(It.IsAny<string>()), Times.Never);
    }

    private static string RolesOn(string methodName)
    {
        return typeof(OperationsController).GetMethod(methodName)!.GetCustomAttribute<PermissionsAttribute>()!.Roles;
    }
}
