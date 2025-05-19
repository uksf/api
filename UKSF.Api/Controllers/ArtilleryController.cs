using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Models.Request;

namespace UKSF.Api.Controllers;

[Route("artillery/{key}")]
[Permissions(Permissions.Member)]
public class ArtilleryController(IArtilleryContext artilleryContext) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public ActionResult<DomainArtillery> Get([FromRoute] string key)
    {
        var artillery = artilleryContext.GetSingle(key);
        return artillery ?? new DomainArtillery { Key = key, Data = "{}" };
    }

    [HttpPut]
    [Authorize]
    public async Task<ActionResult> Put([FromRoute] string key, [FromBody] UpdateArtilleryRequest request)
    {
        var artillery = artilleryContext.GetSingle(key);
        if (artillery == null)
        {
            artillery = new DomainArtillery { Key = key, Data = request.Data };
            await artilleryContext.Add(artillery);
        }
        else
        {
            await artilleryContext.Update(artillery.Id, x => x.Data, request.Data);
        }

        return Ok();
    }
}
