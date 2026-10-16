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

public class CampaignMissionsControllerTests
{
    private readonly Mock<ICampaignMissionsContext> _mockMissions = new();
    private readonly Mock<IOperationsContext> _mockOperations = new();
    private readonly Mock<ICampaignsContext> _mockCampaigns = new();
    private readonly Mock<ICampaignMissionsService> _mockService = new();
    private readonly Mock<IGameServersService> _mockGameServers = new();
    private readonly Mock<IHttpContextService> _mockHttp = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly CampaignMissionsController _controller;

    public CampaignMissionsControllerTests()
    {
        _mockHttp.Setup(x => x.UserHasPermission(Permissions.Command)).Returns(true);
        _mockCampaigns.Setup(x => x.GetSingle("c1"))
        .Returns(
            new DomainCampaign
            {
                Id = "c1",
                Name = "Storm",
                Status = CampaignStatus.Current
            }
        );
        _mockOperations.Setup(x => x.GetSingle("op1"))
        .Returns(
            new DomainOperation
            {
                Id = "op1",
                CampaignId = "c1",
                Title = "Alpha",
                Status = OperationStatus.Current
            }
        );
        _mockGameServers.Setup(x => x.GetServer(It.IsAny<string>())).Returns(new DomainGameServer { Name = "Main Server" });
        _mockService.Setup(x => x.ToDto(It.IsAny<DomainMission>()))
                    .Returns<DomainMission>(m => new MissionDto { Mission = m, MissionFileState = MissionFileState.Present });
        _controller = new CampaignMissionsController(
            _mockMissions.Object,
            _mockOperations.Object,
            _mockCampaigns.Object,
            _mockService.Object,
            _mockGameServers.Object,
            _mockHttp.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public void Nested_route_includes_campaign_and_operation()
    {
        typeof(CampaignMissionsController).GetCustomAttribute<RouteAttribute>()!.Template.Should()
                                          .Be("campaigns/{campaignId}/operations/{operationId}/missions");
    }

    [Fact]
    public void Controller_requires_member_permission()
    {
        var attribute = typeof(CampaignMissionsController).GetCustomAttributes(typeof(PermissionsAttribute), inherit: false)
                                                          .Cast<PermissionsAttribute>()
                                                          .Single();
        attribute.Roles.Should().Be(Permissions.Member);
    }

    [Fact]
    public void Mutations_require_command_and_launch_requires_nco_servers_or_command()
    {
        RolesOn(nameof(CampaignMissionsController.Post)).Should().Be(Permissions.Command);
        RolesOn(nameof(CampaignMissionsController.Put)).Should().Be(Permissions.Command);
        RolesOn(nameof(CampaignMissionsController.Delete)).Should().Be(Permissions.Command);
        RolesOn(nameof(CampaignMissionsController.Launch)).Split(',').Should().BeEquivalentTo(Permissions.Nco, Permissions.Servers, Permissions.Command);
    }

    [Fact]
    public void Get_returns_missions_for_operation()
    {
        DomainMission a = new()
        {
            Id = "m1",
            OperationId = "op1",
            Title = "A",
            MissionName = ""
        };
        DomainMission b = new()
        {
            Id = "m2",
            OperationId = "op2",
            Title = "B"
        };
        _mockMissions.Setup(x => x.Get(It.IsAny<Func<DomainMission, bool>>())).Returns<Func<DomainMission, bool>>(predicate => new[] { a, b }.Where(predicate));

        var result = _controller.Get("c1", "op1").ToList();

        result.Should().ContainSingle();
        result[0].Mission.Should().Be(a);
    }

    [Fact]
    public void Get_throws_when_operation_belongs_to_another_campaign()
    {
        _mockOperations.Setup(x => x.GetSingle("op2")).Returns(new DomainOperation { Id = "op2", CampaignId = "c2" });

        var act = () => _controller.Get("c1", "op2");

        act.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void Get_returns_missions_of_an_upcoming_campaign_to_members()
    {
        _mockHttp.Setup(x => x.UserHasPermission(Permissions.Command)).Returns(false);
        _mockCampaigns.Setup(x => x.GetSingle("c1")).Returns(new DomainCampaign { Id = "c1", Status = CampaignStatus.Upcoming });
        DomainMission mission = new() { Id = "m1", OperationId = "op1" };
        _mockMissions.Setup(x => x.Get(It.IsAny<Func<DomainMission, bool>>())).Returns<Func<DomainMission, bool>>(new[] { mission }.Where);

        var result = _controller.Get("c1", "op1").Select(x => x.Mission);

        result.Should().ContainSingle().Which.Should().Be(mission);
    }

    [Fact]
    public void GetByCampaign_joins_through_operations_and_isolates_siblings()
    {
        DomainOperation own = new() { Id = "op1", CampaignId = "c1" };
        DomainOperation sibling = new() { Id = "op9", CampaignId = "c2" };
        _mockOperations.Setup(x => x.Get(It.IsAny<Func<DomainOperation, bool>>()))
                       .Returns<Func<DomainOperation, bool>>(predicate => new[] { own, sibling }.Where(predicate));
        DomainMission a = new() { Id = "m1", OperationId = "op1" };
        DomainMission other = new() { Id = "m9", OperationId = "op9" };
        _mockMissions.Setup(x => x.Get(It.IsAny<Func<DomainMission, bool>>()))
                     .Returns<Func<DomainMission, bool>>(predicate => new[] { a, other }.Where(predicate));

        var result = _controller.GetByCampaign("c1").ToList();

        result.Should().ContainSingle();
        result[0].Mission.Should().Be(a);
    }

    [Fact]
    public void GetById_returns_dto_when_ancestors_match()
    {
        DomainMission mission = new()
        {
            Id = "m1",
            OperationId = "op1",
            Title = "A",
            MissionName = ""
        };
        _mockMissions.Setup(x => x.GetSingle("m1")).Returns(mission);

        var result = _controller.GetById("c1", "op1", "m1");

        result.Mission.Should().Be(mission);
    }

    [Fact]
    public void GetById_throws_when_mission_belongs_to_another_operation()
    {
        _mockMissions.Setup(x => x.GetSingle("m1")).Returns(new DomainMission { Id = "m1", OperationId = "op2" });

        var act = () => _controller.GetById("c1", "op1", "m1");

        act.Should().Throw<NotFoundException>();
    }

    private static string RolesOn(string methodName)
    {
        return typeof(CampaignMissionsController).GetMethod(methodName)!.GetCustomAttribute<PermissionsAttribute>()!.Roles;
    }
}
