using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Member)]
public class CampaignsController(ICampaignsContext campaignsContext) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IEnumerable<DomainCampaign> Get()
    {
        return campaignsContext.Get();
    }

    [HttpGet("{id}")]
    [Authorize]
    public DomainCampaign Get([FromRoute] string id)
    {
        return campaignsContext.GetSingle(id);
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
        await campaignsContext.Delete(id);
    }
}
