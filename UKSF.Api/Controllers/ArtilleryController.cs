using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Controllers;

[Route("artillery")]
[Permissions(Permissions.Member)]
public class ArtilleryController(IArtilleryContext artilleryContext) : ControllerBase
{
    [HttpGet("{key}")]
    [Authorize]
    public ActionResult<DomainArtillery> Get([FromRoute] string key)
    {
        var artillery = artilleryContext.GetSingle(key);
        if (artillery == null)
        {
            return new DomainArtillery { Key = key, Data = "{}" };
        }

        return artillery;
    }

    [HttpPut("{key}")]
    [Authorize]
    public async Task<ActionResult> Put([FromRoute] string key, [FromBody] string data)
    {
        var artillery = artilleryContext.GetSingle(key);
        if (artillery == null)
        {
            artillery = new DomainArtillery { Key = key, Data = data };
            await artilleryContext.Add(artillery);
        }
        else
        {
            await artilleryContext.Update(artillery.Id, x => x.Data, data);
        }

        return Ok();
    }
}
