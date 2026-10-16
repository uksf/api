using System;
using System.Linq;
using FluentAssertions;
using Moq;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class CampaignOrderingTests
{
    private readonly Mock<ICampaignMissionsContext> _missions = new();
    private readonly Mock<IOperationsContext> _operations = new();
    private readonly Mock<ICampaignsContext> _campaigns = new();
    private readonly Mock<ICampaignMissionsService> _missionsService = new();
    private readonly Mock<IHttpContextService> _http = new();

    public CampaignOrderingTests()
    {
        _http.Setup(x => x.UserHasPermission(Permissions.Command)).Returns(true);
        _campaigns.Setup(x => x.GetSingle("c1")).Returns(new DomainCampaign { Id = "c1", Status = CampaignStatus.Current });
        _missionsService.Setup(x => x.ToDto(It.IsAny<DomainMission>())).Returns<DomainMission>(m => new MissionDto { Mission = m });
    }

    private void GivenOperations(params DomainOperation[] operations)
    {
        _operations.Setup(x => x.Get(It.IsAny<Func<DomainOperation, bool>>())).Returns<Func<DomainOperation, bool>>(operations.Where);
        _operations.Setup(x => x.GetSingle(It.IsAny<string>())).Returns<string>(id => operations.Single(x => x.Id == id));
    }

    private void GivenMissions(params DomainMission[] missions) =>
        _missions.Setup(x => x.Get(It.IsAny<Func<DomainMission, bool>>())).Returns<Func<DomainMission, bool>>(missions.Where);

    private static DomainOperation Operation(string id) => new() { Id = id, CampaignId = "c1" };

    private static DomainMission Mission(string id, string operationId, int day) =>
        new()
        {
            Id = id,
            OperationId = operationId,
            ScheduledTime = new DateTime(2026, 10, day, 19, 0, 0, DateTimeKind.Utc)
        };

    private OperationsController OperationsController() =>
        new(_operations.Object, _campaigns.Object, _missions.Object, Mock.Of<IOperationsService>(), Mock.Of<IUksfLogger>());

    private CampaignMissionsController MissionsController() =>
        new(
            _missions.Object,
            _operations.Object,
            _campaigns.Object,
            _missionsService.Object,
            Mock.Of<IGameServersService>(),
            _http.Object,
            Mock.Of<IUksfLogger>()
        );

    [Fact]
    public void Operations_are_ordered_by_their_earliest_mission_with_mission_less_operations_last()
    {
        GivenOperations(Operation("empty"), Operation("late"), Operation("early"));
        GivenMissions(Mission("m1", "late", 20), Mission("m2", "early", 25), Mission("m3", "early", 5), Mission("m4", "late", 12));

        var result = OperationsController().Get("c1").Select(x => x.Id);

        result.Should().Equal("early", "late", "empty");
    }

    [Fact]
    public void Operation_missions_are_ordered_by_scheduled_time()
    {
        GivenOperations(Operation("op1"));
        GivenMissions(Mission("third", "op1", 30), Mission("first", "op1", 1), Mission("second", "op1", 15));

        var result = MissionsController().Get("c1", "op1").Select(x => x.Mission.Id);

        result.Should().Equal("first", "second", "third");
    }

    [Fact]
    public void Campaign_missions_are_ordered_by_scheduled_time()
    {
        GivenOperations(Operation("op1"), Operation("op2"));
        GivenMissions(Mission("third", "op1", 30), Mission("first", "op2", 1), Mission("second", "op1", 15));

        var result = MissionsController().GetByCampaign("c1").Select(x => x.Mission.Id);

        result.Should().Equal("first", "second", "third");
    }
}
