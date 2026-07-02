using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Member)]
public class IntelPagesController(
    IIntelPagesContext intelPagesContext,
    ICampaignsContext campaignsContext,
    IOpsContext opsContext,
    IUksfLogger logger
) : ControllerBase
{
    private string OwnerLabel(DomainIntelPage page)
    {
        return page.Scope == IntelScope.Campaign
            ? $"campaign '{campaignsContext.GetSingle(page.OwnerId).Name}'"
            : $"op '{opsContext.GetSingle(page.OwnerId).Title}'";
    }

    [HttpGet]
    [Authorize]
    public IEnumerable<DomainIntelPage> Get([FromQuery] IntelScope scope, [FromQuery] string ownerId)
    {
        return intelPagesContext.Get().Where(x => x.Scope == scope && x.OwnerId == ownerId);
    }

    [HttpGet("{id}")]
    [Authorize]
    public DomainIntelPage Get([FromRoute] string id)
    {
        return intelPagesContext.GetSingle(id);
    }

    [HttpPost]
    [Permissions(Permissions.Command)]
    public async Task Post([FromBody] DomainIntelPage page)
    {
        await intelPagesContext.Add(page);
        logger.LogAudit($"Intel page '{page.Title}' added for {OwnerLabel(page)}");
    }

    [HttpPut]
    [Permissions(Permissions.Command)]
    public async Task Put([FromBody] DomainIntelPage page)
    {
        await intelPagesContext.Replace(page);
        logger.LogAudit($"Intel page '{page.Title}' updated for {OwnerLabel(page)}");
    }

    [HttpDelete("{id}")]
    [Permissions(Permissions.Command)]
    public async Task Delete([FromRoute] string id)
    {
        var page = intelPagesContext.GetSingle(id);
        await intelPagesContext.Delete(id);
        logger.LogAudit($"Intel page '{page.Title}' deleted for {OwnerLabel(page)}");
    }
}
