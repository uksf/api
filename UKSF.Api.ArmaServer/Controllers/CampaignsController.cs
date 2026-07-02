using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Member)]
public class CampaignsController(
    ICampaignsContext campaignsContext,
    IOpsContext opsContext,
    IIntelPagesContext intelPagesContext,
    IOpsService opsService,
    IHttpContextService httpContextService
) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IEnumerable<DomainCampaign> Get()
    {
        var campaigns = campaignsContext.Get();
        if (!httpContextService.UserHasPermission(Permissions.Command))
        {
            campaigns = campaigns.Where(x => x.Status != CampaignStatus.Upcoming);
        }

        return campaigns;
    }

    [HttpGet("{id}")]
    [Authorize]
    public DomainCampaign Get([FromRoute] string id)
    {
        var campaign = campaignsContext.GetSingle(id);
        if (campaign is { Status: CampaignStatus.Upcoming } && !httpContextService.UserHasPermission(Permissions.Command))
        {
            throw new NotFoundException("Campaign not found");
        }

        return campaign;
    }

    [HttpPost]
    [Permissions(Permissions.Command)]
    public async Task Post([FromBody] DomainCampaign campaign)
    {
        await campaignsContext.Add(campaign);
    }

    [HttpPut]
    [Permissions(Permissions.Command)]
    public async Task Put([FromBody] DomainCampaign campaign)
    {
        await campaignsContext.Replace(campaign);
    }

    [HttpDelete("{id}")]
    [Permissions(Permissions.Command)]
    public async Task Delete([FromRoute] string id)
    {
        foreach (var op in opsContext.Get(x => x.CampaignId == id).ToList())
        {
            await opsService.DeleteOp(op.Id);
        }

        await intelPagesContext.DeleteMany(x => x.Scope == IntelScope.Campaign && x.OwnerId == id);
        await campaignsContext.Delete(id);
    }
}
