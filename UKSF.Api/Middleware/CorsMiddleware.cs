using Microsoft.Extensions.Options;
using UKSF.Api.Core.Configuration;

namespace UKSF.Api.Middleware;

public class CorsMiddleware(IOptions<AppSettings> appSettingsOptions) : IMiddleware
{
    // Must match the origins defined in Program.cs
    private static readonly string[] DevelopmentOrigins =
        ["http://localhost:4200", "http://localhost:4300", "https://dev.uk-sf.co.uk", "https://api-dev.uk-sf.co.uk"];

    private static readonly string[] ProductionOrigins = ["https://uk-sf.co.uk", "https://api.uk-sf.co.uk"];

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Path.Value is not null && context.Request.Path.Value.Contains("hub"))
        {
            var origin = context.Request.Headers.Origin.ToString();
            if (!string.IsNullOrEmpty(origin) && IsAllowedOrigin(origin))
            {
                context.Response.Headers["Access-Control-Allow-Origin"] = origin;
                context.Response.Headers["Access-Control-Allow-Credentials"] = "true";

                if (HttpMethods.IsOptions(context.Request.Method))
                {
                    context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
                    context.Response.Headers["Access-Control-Allow-Headers"] = "Authorization, Content-Type, x-requested-with, x-signalr-user-agent";
                    context.Response.StatusCode = StatusCodes.Status204NoContent;
                    return;
                }
            }
        }

        await next(context);
    }

    private bool IsAllowedOrigin(string origin)
    {
        var allowedOrigins = appSettingsOptions.Value.Environment switch
        {
            "Development" => DevelopmentOrigins,
            "Production"  => ProductionOrigins,
            _             => []
        };

        return allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
    }
}
