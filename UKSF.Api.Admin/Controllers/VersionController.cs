using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Admin.Context;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Signalr.Clients;
using UKSF.Api.Admin.Signalr.Hubs;

namespace UKSF.Api.Admin.Controllers;

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
