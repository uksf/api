using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Member)]
public class IntelPagesController(
    IIntelPagesContext intelPagesContext,
    ICampaignsContext campaignsContext,
    IOperationsContext operationsContext,
    ICampaignMissionsContext campaignMissionsContext,
    IHttpContextService httpContextService,
    IUksfLogger logger
) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IEnumerable<DomainIntelPage> Get([FromQuery] IntelScope scope, [FromQuery] string ownerId)
    {
        RequireOwner(scope, ownerId);
        return intelPagesContext.Get().Where(x => x.Scope == scope && x.OwnerId == ownerId);
    }

    [HttpGet("{id}")]
    [Authorize]
    public DomainIntelPage Get([FromRoute] string id)
    {
        var page = intelPagesContext.GetSingle(id);
        if (page is null)
        {
            throw new NotFoundException("Intel page not found");
        }

        RequireOwner(page.Scope, page.OwnerId);
        return page;
    }

    [HttpPost]
    [Permissions(Permissions.Command)]
    public async Task Post([FromBody] DomainIntelPage page)
    {
        RequireOwner(page.Scope, page.OwnerId, enforceVisibility: false);
        await intelPagesContext.Add(page);
        logger.LogAudit($"Intel page '{page.Title}' added for {OwnerLabel(page)}");
    }

    [HttpPut]
    [Permissions(Permissions.Command)]
    public async Task Put([FromBody] DomainIntelPage page)
    {
        var stored = intelPagesContext.GetSingle(page.Id);
        if (stored is null)
        {
            throw new NotFoundException("Intel page not found");
        }

        RequireOwner(stored.Scope, stored.OwnerId, enforceVisibility: false);
        RequireOwner(page.Scope, page.OwnerId, enforceVisibility: false);
        await intelPagesContext.Replace(page);
        logger.LogAudit($"Intel page '{page.Title}' updated for {OwnerLabel(page)}");
    }

    [HttpDelete("{id}")]
    [Permissions(Permissions.Command)]
    public async Task Delete([FromRoute] string id)
    {
        var page = intelPagesContext.GetSingle(id);
        if (page is null)
        {
            throw new NotFoundException("Intel page not found");
        }

        RequireOwner(page.Scope, page.OwnerId, enforceVisibility: false);
        await intelPagesContext.Delete(id);
        logger.LogAudit($"Intel page '{page.Title}' deleted for {OwnerLabel(page)}");
    }

    private string OwnerLabel(DomainIntelPage page)
    {
        return page.Scope switch
        {
            IntelScope.Campaign => $"campaign '{campaignsContext.GetSingle(page.OwnerId)?.Name}'",
            IntelScope.Operation => $"operation '{operationsContext.GetSingle(page.OwnerId)?.Title}'",
            IntelScope.Mission => $"mission '{campaignMissionsContext.GetSingle(page.OwnerId)?.Title}'",
            _ => "unknown"
        };
    }

    private void RequireOwner(IntelScope scope, string ownerId, bool enforceVisibility = true)
    {
        if (!Enum.IsDefined(scope))
        {
            throw new BadRequestException("Invalid intel scope");
        }

        var campaign = ResolveOwnerCampaign(scope, ownerId);
        if (campaign is null)
        {
            throw enforceVisibility ? new NotFoundException("Intel owner not found") : new BadRequestException("Intel owner not found");
        }

        if (enforceVisibility && campaign.Status == CampaignStatus.Upcoming && !httpContextService.UserHasPermission(Permissions.Command))
        {
            throw new NotFoundException("Campaign not found");
        }
    }

    private DomainCampaign ResolveOwnerCampaign(IntelScope scope, string ownerId)
    {
        switch (scope)
        {
            case IntelScope.Campaign:
                return campaignsContext.GetSingle(ownerId);
            case IntelScope.Operation:
            {
                var operation = operationsContext.GetSingle(ownerId);
                return operation is null ? null : campaignsContext.GetSingle(operation.CampaignId);
            }
            case IntelScope.Mission:
            {
                var mission = campaignMissionsContext.GetSingle(ownerId);
                if (mission is null)
                {
                    return null;
                }

                var operation = operationsContext.GetSingle(mission.OperationId);
                return operation is null ? null : campaignsContext.GetSingle(operation.CampaignId);
            }
            default:
                return null;
        }
    }
}
