using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("campaigns/{campaignId}/operations/{operationId}/missions")]
[Permissions(Permissions.Member)]
public class CampaignMissionsController(
    ICampaignMissionsContext campaignMissionsContext,
    IOperationsContext operationsContext,
    ICampaignsContext campaignsContext,
    ICampaignMissionsService campaignMissionsService,
    IGameServersService gameServersService,
    IHttpContextService httpContextService,
    IUksfLogger logger
) : ControllerBase
{
    [HttpGet("~/campaigns/{campaignId}/missions")]
    [Authorize]
    public IEnumerable<MissionDto> GetByCampaign([FromRoute] string campaignId)
    {
        RequireVisibleCampaign(campaignId);
        var operationIds = operationsContext.Get(x => x.CampaignId == campaignId).Select(x => x.Id).ToHashSet();
        return campaignMissionsContext.Get(x => operationIds.Contains(x.OperationId)).Select(campaignMissionsService.ToDto);
    }

    [HttpGet]
    [Authorize]
    public IEnumerable<MissionDto> Get([FromRoute] string campaignId, [FromRoute] string operationId)
    {
        RequireOperation(campaignId, operationId);
        return campaignMissionsContext.Get(x => x.OperationId == operationId).Select(campaignMissionsService.ToDto);
    }

    [HttpGet("{missionId}")]
    [Authorize]
    public MissionDto GetById([FromRoute] string campaignId, [FromRoute] string operationId, [FromRoute] string missionId)
    {
        return campaignMissionsService.ToDto(RequireMission(campaignId, operationId, missionId));
    }

    [HttpPost]
    [Permissions(Permissions.Command)]
    public async Task Post([FromRoute] string campaignId, [FromRoute] string operationId, [FromBody] DomainMission mission)
    {
        var operation = RequireOperation(campaignId, operationId);
        if (!string.IsNullOrEmpty(mission.OperationId) && mission.OperationId != operationId)
        {
            throw new BadRequestException("Cannot reparent mission");
        }

        mission.OperationId = operation.Id;
        campaignMissionsService.ApplyDefaults(mission);
        await campaignMissionsContext.Add(mission);
        logger.LogAudit($"Mission '{mission.Title}' added for operation '{operation.Title}'");
    }

    [HttpPut("{missionId}")]
    [Permissions(Permissions.Command)]
    public async Task Put(
        [FromRoute] string campaignId,
        [FromRoute] string operationId,
        [FromRoute] string missionId,
        [FromBody] DomainMission mission
    )
    {
        var stored = RequireMission(campaignId, operationId, missionId);
        if (!string.IsNullOrEmpty(mission.Id) && mission.Id != missionId)
        {
            throw new BadRequestException("Mission id mismatch");
        }

        if (!string.IsNullOrEmpty(mission.OperationId) && mission.OperationId != operationId)
        {
            throw new BadRequestException("Cannot reparent mission");
        }

        mission.Id = stored.Id;
        mission.OperationId = operationId;
        await campaignMissionsContext.Replace(mission);
        logger.LogAudit($"Mission '{mission.Title}' updated for operation '{operationsContext.GetSingle(operationId).Title}'");
    }

    [HttpDelete("{missionId}")]
    [Permissions(Permissions.Command)]
    public async Task Delete([FromRoute] string campaignId, [FromRoute] string operationId, [FromRoute] string missionId)
    {
        var mission = RequireMission(campaignId, operationId, missionId);
        await campaignMissionsService.DeleteMission(missionId);
        logger.LogAudit($"Mission '{mission.Title}' deleted for operation '{operationsContext.GetSingle(operationId).Title}'");
    }

    [HttpPost("{missionId}/launch")]
    [Permissions(Permissions.Nco, Permissions.Servers, Permissions.Command)]
    public async Task<List<ValidationReport>> Launch(
        [FromRoute] string campaignId,
        [FromRoute] string operationId,
        [FromRoute] string missionId
    )
    {
        var mission = RequireMission(campaignId, operationId, missionId);
        var reports = await campaignMissionsService.LaunchMissionAsync(mission, httpContextService.GetUserId());
        logger.LogAudit($"Mission '{mission.Title}' launched '{mission.MissionName}' on '{gameServersService.GetServer(mission.ServerId).Name}'");
        return reports;
    }

    private DomainCampaign RequireVisibleCampaign(string campaignId)
    {
        var campaign = campaignsContext.GetSingle(campaignId);
        if (campaign is null)
        {
            throw new NotFoundException("Campaign not found");
        }

        if (campaign.Status == CampaignStatus.Upcoming && !httpContextService.UserHasPermission(Permissions.Command))
        {
            throw new NotFoundException("Campaign not found");
        }

        return campaign;
    }

    private DomainOperation RequireOperation(string campaignId, string operationId)
    {
        RequireVisibleCampaign(campaignId);
        var operation = operationsContext.GetSingle(operationId);
        if (operation is null || operation.CampaignId != campaignId)
        {
            throw new NotFoundException("Operation not found");
        }

        return operation;
    }

    private DomainMission RequireMission(string campaignId, string operationId, string missionId)
    {
        RequireOperation(campaignId, operationId);
        var mission = campaignMissionsContext.GetSingle(missionId);
        if (mission is null || mission.OperationId != operationId)
        {
            throw new NotFoundException("Mission not found");
        }

        return mission;
    }
}
