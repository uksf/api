using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace UKSF.Api.ArmaServer.Controllers;

/// <summary>
/// Rejects any request that didn't originate from a direct localhost connection.
/// Requests routed through the reverse proxy carry an X-Forwarded-For header, so
/// their presence means the request came from the internet, not from a local game server.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class LocalhostOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.HttpContext.Request.Headers.ContainsKey("X-Forwarded-For"))
        {
            context.Result = new ObjectResult("Only direct localhost connections are allowed") { StatusCode = 403 };
            return;
        }

        var remoteIp = context.HttpContext.Connection.RemoteIpAddress;
        if (remoteIp is null || !IPAddress.IsLoopback(remoteIp))
        {
            context.Result = new ObjectResult("Only direct localhost connections are allowed") { StatusCode = 403 };
        }
    }
}
