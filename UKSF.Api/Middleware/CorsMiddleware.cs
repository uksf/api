using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace UKSF.Api.Middleware
{
    public class CorsMiddleware : IMiddleware
    {
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.Request.Path.Value != null && context.Request.Path.Value.Contains("hub"))
            {
                context.Response.Headers["Access-Control-Allow-Origin"] = context.Request.Headers["Origin"];
                context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
            }

            await next(context);
        }
    }
}
