using System.Net;
using Microsoft.AspNetCore.Http;

namespace UKSF.Api.ArmaServer.Middleware;

public class LoopbackOnlyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress;
        if (ip is null || (!IPAddress.IsLoopback(ip)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context);
    }
}
