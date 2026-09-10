using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("campaigns/{campaignId}/operations")]
[Permissions(Permissions.Member)]
public class OperationsController(
    IOperationsContext operationsContext,
    ICampaignsContext campaignsContext,
    IOperationsService operationsService,
    IHttpContextService httpContextService,
    IUksfLogger logger
) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IEnumerable<DomainOperation> Get([FromRoute] string campaignId)
    {
        RequireVisibleCampaign(campaignId);
        return operationsContext.Get(x => x.CampaignId == campaignId);
    }

    [HttpGet("{operationId}")]
    [Authorize]
    public DomainOperation GetById([FromRoute] string campaignId, [FromRoute] string operationId)
    {
        return RequireOperation(campaignId, operationId);
    }

    [HttpPost]
    [Permissions(Permissions.Command)]
    public async Task Post([FromRoute] string campaignId, [FromBody] DomainOperation operation)
    {
        var campaign = RequireVisibleCampaign(campaignId);
        if (!string.IsNullOrEmpty(operation.CampaignId) && operation.CampaignId != campaignId)
        {
            throw new BadRequestException("Cannot reparent operation");
        }

        operation.CampaignId = campaignId;
        await operationsContext.Add(operation);
        logger.LogAudit($"Operation '{operation.Title}' added for campaign '{campaign.Name}'");
    }

    [HttpPut("{operationId}")]
    [Permissions(Permissions.Command)]
    public async Task Put([FromRoute] string campaignId, [FromRoute] string operationId, [FromBody] DomainOperation operation)
    {
        var stored = RequireOperation(campaignId, operationId);
        if (!string.IsNullOrEmpty(operation.Id) && operation.Id != operationId)
        {
            throw new BadRequestException("Operation id mismatch");
        }

        if (!string.IsNullOrEmpty(operation.CampaignId) && operation.CampaignId != campaignId)
        {
            throw new BadRequestException("Cannot reparent operation");
        }

        operation.Id = stored.Id;
        operation.CampaignId = campaignId;
        await operationsContext.Replace(operation);
        logger.LogAudit($"Operation '{operation.Title}' updated for campaign '{campaignsContext.GetSingle(campaignId).Name}'");
    }

    [HttpDelete("{operationId}")]
    [Permissions(Permissions.Command)]
    public async Task Delete([FromRoute] string campaignId, [FromRoute] string operationId)
    {
        var operation = RequireOperation(campaignId, operationId);
        await operationsService.DeleteOperation(operationId);
        logger.LogAudit($"Operation '{operation.Title}' deleted for campaign '{campaignsContext.GetSingle(campaignId).Name}'");
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
}
