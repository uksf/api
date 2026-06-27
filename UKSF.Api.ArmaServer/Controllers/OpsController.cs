using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Core;

namespace UKSF.Api.ArmaServer.Controllers;

[Route("[controller]")]
[Permissions(Permissions.Member)]
public class OpsController(IOpsContext opsContext, IOpsService opsService) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IEnumerable<OpDto> Get([FromQuery] string campaignId)
    {
        var ops = opsContext.Get();
        if (!string.IsNullOrEmpty(campaignId))
        {
            ops = ops.Where(x => x.CampaignId == campaignId);
        }

        return ops.Select(opsService.ToDto);
    }

    [HttpGet("{id}")]
    [Authorize]
    public OpDto GetById([FromRoute] string id)
    {
        return opsService.ToDto(opsContext.GetSingle(id));
    }

    [HttpPost]
    [Permissions(Permissions.Command)]
    public async Task Post([FromBody] DomainOp op)
    {
        opsService.ApplyDefaults(op);
        await opsContext.Add(op);
    }

    [HttpPut]
    [Permissions(Permissions.Command)]
    public async Task Put([FromBody] DomainOp op)
    {
        await opsContext.Replace(op);
    }

    [HttpDelete("{id}")]
    [Permissions(Permissions.Command)]
    public async Task Delete([FromRoute] string id)
    {
        await opsContext.Delete(id);
    }
}
