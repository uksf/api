using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core.Exceptions;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Integration;

public class CampaignHierarchyMongoTests : IDisposable
{
    private readonly CampaignHierarchyMongoHarness _harness = new();

    public void Dispose()
    {
        _harness.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Creates_hierarchy_projects_missions_and_rejects_wrong_ancestry()
    {
        var campaignId = CampaignHierarchyMongoHarness.NewId();
        var otherCampaignId = CampaignHierarchyMongoHarness.NewId();
        await _harness.CampaignsController.Post(new DomainCampaign { Id = campaignId, Name = "Storm", Status = CampaignStatus.Current });
        await _harness.CampaignsController.Post(new DomainCampaign { Id = otherCampaignId, Name = "Other", Status = CampaignStatus.Current });

        DomainOperation op1 = new() { Title = "Alpha", Brief = "A", Status = OperationStatus.Current };
        DomainOperation op2 = new() { Title = "Bravo", Brief = "B", Status = OperationStatus.Upcoming };
        await _harness.OperationsController.Post(campaignId, op1);
        await _harness.OperationsController.Post(campaignId, op2);
        DomainOperation otherOp = new() { Title = "OtherOp" };
        await _harness.OperationsController.Post(otherCampaignId, otherOp);

        await _harness.CampaignMissionsController.Post(campaignId, op1.Id, new DomainMission { Title = "M1", MissionName = "" });
        await _harness.CampaignMissionsController.Post(campaignId, op1.Id, new DomainMission { Title = "M2", MissionName = "m.Altis.pbo" });
        await _harness.CampaignMissionsController.Post(campaignId, op2.Id, new DomainMission { Title = "M3", MissionName = "" });
        await _harness.CampaignMissionsController.Post(otherCampaignId, otherOp.Id, new DomainMission { Title = "OtherM" });

        var projected = _harness.CampaignMissionsController.GetByCampaign(campaignId).ToList();
        projected.Select(x => x.Mission.Title).Should().BeEquivalentTo("M1", "M2", "M3");
        projected.Should().OnlyContain(x => x.Mission.OperationId == op1.Id || x.Mission.OperationId == op2.Id);

        var storedOp = _harness.Database.GetCollection<BsonDocument>("operations").Find(Builders<BsonDocument>.Filter.Eq("title", "Alpha")).First();
        storedOp["campaignId"].BsonType.Should().Be(BsonType.ObjectId);
        storedOp["campaignId"].AsObjectId.ToString().Should().Be(campaignId);

        var m1 = projected.Single(x => x.Mission.Title == "M1").Mission;
        var actWrongCampaign = () => _harness.CampaignMissionsController.GetById(otherCampaignId, op1.Id, m1.Id);
        var actWrongOperation = () => _harness.CampaignMissionsController.GetById(campaignId, op2.Id, m1.Id);
        var actMissing = () => _harness.CampaignMissionsController.GetById(campaignId, op1.Id, CampaignHierarchyMongoHarness.NewId());
        actWrongCampaign.Should().Throw<NotFoundException>();
        actWrongOperation.Should().Throw<NotFoundException>();
        actMissing.Should().Throw<NotFoundException>();
    }

    [Fact]
    public async Task Intel_at_all_three_scopes_persists_and_lists_by_owner()
    {
        var campaignId = CampaignHierarchyMongoHarness.NewId();
        await _harness.CampaignsController.Post(new DomainCampaign { Id = campaignId, Name = "Storm", Status = CampaignStatus.Current });
        DomainOperation operation = new() { Title = "Alpha" };
        await _harness.OperationsController.Post(campaignId, operation);
        DomainMission mission = new() { Title = "M1", MissionName = "" };
        await _harness.CampaignMissionsController.Post(campaignId, operation.Id, mission);

        await _harness.IntelPagesController.Post(new DomainIntelPage { Scope = IntelScope.Campaign, OwnerId = campaignId, Title = "C" });
        await _harness.IntelPagesController.Post(new DomainIntelPage { Scope = IntelScope.Operation, OwnerId = operation.Id, Title = "O" });
        await _harness.IntelPagesController.Post(new DomainIntelPage { Scope = IntelScope.Mission, OwnerId = mission.Id, Title = "M" });

        _harness.IntelPagesController.Get(IntelScope.Campaign, campaignId).Select(x => x.Title).Should().Equal("C");
        _harness.IntelPagesController.Get(IntelScope.Operation, operation.Id).Select(x => x.Title).Should().Equal("O");
        _harness.IntelPagesController.Get(IntelScope.Mission, mission.Id).Select(x => x.Title).Should().Equal("M");

        var stored = _harness.Database.GetCollection<BsonDocument>("intelPages").Find(FilterDefinition<BsonDocument>.Empty).ToList();
        stored.Should().HaveCount(3);
        stored.Should().OnlyContain(x => x["ownerId"].BsonType == BsonType.ObjectId);
        stored.Select(x => x["scope"].AsInt32).Should().BeEquivalentTo([0, 1, 2]);
    }

    [Fact]
    public async Task Launch_does_not_change_operation_status()
    {
        var campaignId = CampaignHierarchyMongoHarness.NewId();
        await _harness.CampaignsController.Post(new DomainCampaign { Id = campaignId, Name = "Storm", Status = CampaignStatus.Current });
        DomainOperation operation = new() { Title = "Alpha", Status = OperationStatus.Current };
        await _harness.OperationsController.Post(campaignId, operation);
        DomainMission mission = new() { Title = "M1", MissionName = "m.Altis.pbo" };
        await _harness.CampaignMissionsController.Post(campaignId, operation.Id, mission);

        await _harness.CampaignMissionsController.Launch(campaignId, operation.Id, mission.Id);

        _harness.OperationsController.GetById(campaignId, operation.Id).Status.Should().Be(OperationStatus.Current);
        var updated = _harness.CampaignMissionsController.GetById(campaignId, operation.Id, mission.Id).Mission;
        updated.LaunchedMission.Should().Be("m.Altis.pbo");
        updated.Status.Should().Be(MissionStatus.Scheduled);
        _harness.Launch.Verify(x => x.LaunchAsync(It.IsAny<string>(), "m.Altis.pbo", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Cascades_delete_branch_and_campaign_while_siblings_and_files_survive()
    {
        var campaignId = CampaignHierarchyMongoHarness.NewId();
        var siblingId = CampaignHierarchyMongoHarness.NewId();
        await _harness.CampaignsController.Post(new DomainCampaign { Id = campaignId, Name = "Storm", Status = CampaignStatus.Current });
        await _harness.CampaignsController.Post(new DomainCampaign { Id = siblingId, Name = "Keep", Status = CampaignStatus.Current });

        DomainOperation keepOp = new() { Title = "KeepOp" };
        DomainOperation dropOp = new() { Title = "DropOp" };
        DomainOperation siblingOp = new() { Title = "SiblingOp" };
        await _harness.OperationsController.Post(campaignId, keepOp);
        await _harness.OperationsController.Post(campaignId, dropOp);
        await _harness.OperationsController.Post(siblingId, siblingOp);

        DomainMission keepMission = new() { Title = "KeepM", MissionName = "" };
        DomainMission dropMission = new() { Title = "DropM", MissionName = "" };
        DomainMission siblingMission = new() { Title = "SiblingM", MissionName = "" };
        await _harness.CampaignMissionsController.Post(campaignId, keepOp.Id, keepMission);
        await _harness.CampaignMissionsController.Post(campaignId, dropOp.Id, dropMission);
        await _harness.CampaignMissionsController.Post(siblingId, siblingOp.Id, siblingMission);

        await _harness.IntelPagesController.Post(new DomainIntelPage { Scope = IntelScope.Mission, OwnerId = dropMission.Id, Title = "DropIntel" });
        await _harness.IntelPagesController.Post(new DomainIntelPage { Scope = IntelScope.Mission, OwnerId = siblingMission.Id, Title = "SiblingIntel" });
        await _harness.IntelPagesController.Post(new DomainIntelPage { Scope = IntelScope.Campaign, OwnerId = campaignId, Title = "CampIntel" });
        await _harness.IntelPagesController.Post(new DomainIntelPage { Scope = IntelScope.Campaign, OwnerId = siblingId, Title = "KeepCampIntel" });

        await _harness.OperationsController.Delete(campaignId, dropOp.Id);
        _harness.Missions.GetSingle(dropMission.Id).Should().BeNull();
        _harness.IntelPages.Get(x => x.OwnerId == dropMission.Id).Should().BeEmpty();
        _harness.Missions.GetSingle(keepMission.Id).Should().NotBeNull();
        _harness.Missions.GetSingle(siblingMission.Id).Should().NotBeNull();

        await _harness.CampaignsController.Delete(campaignId);
        _harness.Campaigns.GetSingle(campaignId).Should().BeNull();
        _harness.Operations.Get(x => x.CampaignId == campaignId).Should().BeEmpty();
        _harness.Missions.GetSingle(keepMission.Id).Should().BeNull();
        _harness.Campaigns.GetSingle(siblingId).Should().NotBeNull();
        _harness.Operations.GetSingle(siblingOp.Id).Should().NotBeNull();
        _harness.Missions.GetSingle(siblingMission.Id).Should().NotBeNull();
        _harness.IntelPagesController.Get(IntelScope.Campaign, siblingId).Select(x => x.Title).Should().Equal("KeepCampIntel");
        _harness.FileMissions.Verify(x => x.DeleteMissionFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Partial_operation_delete_failure_leaves_remaining_mission()
    {
        var campaignId = CampaignHierarchyMongoHarness.NewId();
        await _harness.CampaignsController.Post(new DomainCampaign { Id = campaignId, Name = "Storm", Status = CampaignStatus.Current });
        DomainOperation operation = new() { Title = "Alpha" };
        await _harness.OperationsController.Post(campaignId, operation);
        DomainMission first = new() { Title = "First", MissionName = "" };
        DomainMission second = new() { Title = "Second", MissionName = "" };
        await _harness.CampaignMissionsController.Post(campaignId, operation.Id, first);
        await _harness.CampaignMissionsController.Post(campaignId, operation.Id, second);

        var failing = new FailingDelete( _harness.CampaignMissionsService, second.Id);
        OperationsService operationsService = new(_harness.Operations, _harness.Missions, failing, _harness.IntelPages);

        var act = () => operationsService.DeleteOperation(operation.Id);
        await act.Should().ThrowAsync<InvalidOperationException>();

        _harness.Missions.GetSingle(second.Id).Should().NotBeNull();
        _harness.Operations.GetSingle(operation.Id).Should().NotBeNull();
    }

    private sealed class FailingDelete(ICampaignMissionsService inner, string failId) : ICampaignMissionsService
    {
        public void ApplyDefaults(DomainMission mission) => inner.ApplyDefaults(mission);
        public DateTime NextStandardOpTimeUtc(DateTime nowUtc) => inner.NextStandardOpTimeUtc(nowUtc);
        public MissionDto ToDto(DomainMission mission) => inner.ToDto(mission);
        public Task<System.Collections.Generic.List<UKSF.Api.Core.Models.ValidationReport>> LaunchMissionAsync(DomainMission mission, string launchedBy) =>
            inner.LaunchMissionAsync(mission, launchedBy);

        public Task DeleteMission(string id)
        {
            if (id == failId)
            {
                throw new InvalidOperationException("fail");
            }

            return inner.DeleteMission(id);
        }
    }
}
