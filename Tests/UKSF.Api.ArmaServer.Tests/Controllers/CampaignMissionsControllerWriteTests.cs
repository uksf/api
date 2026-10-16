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

public class CampaignMissionsControllerWriteTests
{
    private readonly Mock<ICampaignMissionsContext> _mockMissions = new();
    private readonly Mock<IOperationsContext> _mockOperations = new();
    private readonly Mock<ICampaignsContext> _mockCampaigns = new();
    private readonly Mock<ICampaignMissionsService> _mockService = new();
    private readonly Mock<IGameServersService> _mockGameServers = new();
    private readonly Mock<IHttpContextService> _mockHttp = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly CampaignMissionsController _controller;

    public CampaignMissionsControllerWriteTests()
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
    public async Task Post_accepts_empty_mission_name_and_applies_defaults()
    {
        DomainMission mission = new() { Title = "Alpha", MissionName = "" };

        await _controller.Post("c1", "op1", mission);

        mission.OperationId.Should().Be("op1");
        _mockService.Verify(x => x.ApplyDefaults(mission), Times.Once);
        _mockMissions.Verify(x => x.Add(mission), Times.Once);
    }

    [Fact]
    public async Task Post_rejects_reparenting_payload()
    {
        DomainMission mission = new() { Title = "Alpha", OperationId = "op2" };

        var act = () => _controller.Post("c1", "op1", mission);

        await act.Should().ThrowAsync<BadRequestException>();
        _mockMissions.Verify(x => x.Add(It.IsAny<DomainMission>()), Times.Never);
    }

    [Fact]
    public async Task Put_replaces_when_ids_match()
    {
        _mockMissions.Setup(x => x.GetSingle("m1")).Returns(new DomainMission { Id = "m1", OperationId = "op1" });
        DomainMission payload = new()
        {
            Id = "m1",
            OperationId = "op1",
            Title = "Updated",
            MissionName = ""
        };

        await _controller.Put("c1", "op1", "m1", payload);

        _mockMissions.Verify(x => x.Replace(It.Is<DomainMission>(m => m.Id == "m1" && m.OperationId == "op1" && m.Title == "Updated")), Times.Once);
    }

    [Fact]
    public async Task Put_rejects_body_id_mismatch()
    {
        _mockMissions.Setup(x => x.GetSingle("m1")).Returns(new DomainMission { Id = "m1", OperationId = "op1" });
        DomainMission payload = new()
        {
            Id = "m2",
            OperationId = "op1",
            Title = "A"
        };

        var act = () => _controller.Put("c1", "op1", "m1", payload);

        await act.Should().ThrowAsync<BadRequestException>();
        _mockMissions.Verify(x => x.Replace(It.IsAny<DomainMission>()), Times.Never);
    }

    [Fact]
    public async Task Put_rejects_reparenting_payload()
    {
        _mockMissions.Setup(x => x.GetSingle("m1")).Returns(new DomainMission { Id = "m1", OperationId = "op1" });
        DomainMission payload = new()
        {
            Id = "m1",
            OperationId = "op2",
            Title = "A"
        };

        var act = () => _controller.Put("c1", "op1", "m1", payload);

        await act.Should().ThrowAsync<BadRequestException>();
        _mockMissions.Verify(x => x.Replace(It.IsAny<DomainMission>()), Times.Never);
    }

    [Fact]
    public async Task Put_throws_when_stored_mission_has_wrong_operation()
    {
        _mockMissions.Setup(x => x.GetSingle("m1")).Returns(new DomainMission { Id = "m1", OperationId = "op2" });
        DomainMission payload = new()
        {
            Id = "m1",
            OperationId = "op1",
            Title = "A"
        };

        var act = () => _controller.Put("c1", "op1", "m1", payload);

        await act.Should().ThrowAsync<NotFoundException>();
        _mockMissions.Verify(x => x.Replace(It.IsAny<DomainMission>()), Times.Never);
    }

    [Fact]
    public async Task Delete_delegates_to_service()
    {
        _mockMissions.Setup(x => x.GetSingle("m1"))
        .Returns(
            new DomainMission
            {
                Id = "m1",
                OperationId = "op1",
                Title = "A"
            }
        );

        await _controller.Delete("c1", "op1", "m1");

        _mockService.Verify(x => x.DeleteMission("m1"), Times.Once);
    }

    [Fact]
    public async Task Launch_throws_not_found_when_mission_missing()
    {
        _mockMissions.Setup(x => x.GetSingle("missing")).Returns((DomainMission)null);

        var act = () => _controller.Launch("c1", "op1", "missing");

        await act.Should().ThrowAsync<NotFoundException>();
        _mockService.Verify(x => x.LaunchMissionAsync(It.IsAny<DomainMission>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Launch_delegates_to_service_without_changing_operation_status()
    {
        DomainOperation operation = new()
        {
            Id = "op1",
            CampaignId = "c1",
            Title = "Alpha",
            Status = OperationStatus.Upcoming
        };
        _mockOperations.Setup(x => x.GetSingle("op1")).Returns(operation);
        DomainMission mission = new()
        {
            Id = "m1",
            OperationId = "op1",
            Title = "Alpha",
            ServerId = "s1",
            MissionName = "m.Altis.pbo"
        };
        _mockMissions.Setup(x => x.GetSingle("m1")).Returns(mission);
        _mockHttp.Setup(x => x.GetUserId()).Returns("user1");
        _mockService.Setup(x => x.LaunchMissionAsync(mission, "user1")).ReturnsAsync([]);

        var result = await _controller.Launch("c1", "op1", "m1");

        result.Should().BeEmpty();
        _mockService.Verify(x => x.LaunchMissionAsync(mission, "user1"), Times.Once);
        _mockOperations.Verify(x => x.Replace(It.IsAny<DomainOperation>()), Times.Never);
        operation.Status.Should().Be(OperationStatus.Upcoming);
    }

    [Fact]
    public async Task Launch_throws_when_ancestors_mismatch()
    {
        _mockMissions.Setup(x => x.GetSingle("m1")).Returns(new DomainMission { Id = "m1", OperationId = "op2" });

        var act = () => _controller.Launch("c1", "op1", "m1");

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
