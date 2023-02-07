using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Signalr.Clients;
using UKSF.Api.Signalr.Hubs;

namespace UKSF.Api.Controllers;

[Route("version")]
public class VersionController : ControllerBase
{
    private readonly IHubContext<UtilityHub, IUtilityClient> _utilityHub;
    private readonly IVariablesContext _variablesContext;

    public VersionController(IVariablesContext variablesContext, IHubContext<UtilityHub, IUtilityClient> utilityHub)
    {
        _variablesContext = variablesContext;
        _utilityHub = utilityHub;
    }

    [HttpGet]
    public string GetFrontendVersion()
    {
        return _variablesContext.GetSingle("FRONTEND_VERSION").AsString();
    }

    [HttpGet("update")]
    [Authorize]
    public async Task UpdateFrontendVersion()
    {
        var version = _variablesContext.GetSingle("FRONTEND_VERSION").AsInt();
        var newVersion = version + 1;

        await _variablesContext.Update("FRONTEND_VERSION", newVersion);
        await _utilityHub.Clients.All.ReceiveFrontendUpdate(newVersion.ToString());
    }
}
