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

[Route("[controller]")]
[Permissions(Permissions.Member)]
public class OpsController(
    IOpsContext opsContext,
    IOpsService opsService,
    IGameServerLaunchService gameServerLaunchService,
    IHttpContextService httpContextService
) : ControllerBase
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

    [HttpPost("{id}/launch")]
    [Permissions(Permissions.Nco, Permissions.Servers, Permissions.Command)]
    public async Task<List<ValidationReport>> LaunchOp([FromRoute] string id)
    {
        var op = opsContext.GetSingle(id);
        if (op is null)
        {
            throw new BadRequestException("Op not found");
        }

        var dto = opsService.ToDto(op);
        if (dto.MissionFileState == MissionFileState.Missing)
        {
            throw new BadRequestException("The mission file for this op is missing. Re-assign or restore it before launching.");
        }

        var reports = await gameServerLaunchService.LaunchAsync(op.ServerId, op.MissionName, httpContextService.GetUserId());

        op.LaunchedServerId = op.ServerId;
        op.LaunchedMission = op.MissionName;
        op.LaunchedAt = DateTime.UtcNow;
        await opsContext.Replace(op);

        return reports;
    }
}
