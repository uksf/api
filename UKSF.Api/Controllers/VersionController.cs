using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Signalr.Clients;
using UKSF.Api.Signalr.Hubs;

namespace UKSF.Api.Controllers;

[Route("version")]
public class VersionController(IVariablesContext variablesContext, IHubContext<UtilityHub, IUtilityClient> utilityHub) : ControllerBase
{
    [HttpGet]
    public string GetFrontendVersion()
    {
        return variablesContext.GetSingle("FRONTEND_VERSION").AsString();
    }

    [HttpGet("update")]
    [Authorize]
    public async Task UpdateFrontendVersion()
    {
        var version = variablesContext.GetSingle("FRONTEND_VERSION").AsInt();
        var newVersion = version + 1;

        await variablesContext.Update("FRONTEND_VERSION", newVersion);
        await utilityHub.Clients.All.ReceiveFrontendUpdate(newVersion.ToString());
    }
}
