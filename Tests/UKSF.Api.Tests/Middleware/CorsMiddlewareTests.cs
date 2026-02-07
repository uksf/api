using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Middleware;
using Xunit;

namespace UKSF.Api.Tests.Middleware;

public class CorsMiddlewareTests
{
    private static CorsMiddleware CreateMiddleware(string environment = "Development")
    {
        var appSettings = new AppSettings { Environment = environment };
        return new CorsMiddleware(Options.Create(appSettings));
    }

    private static DefaultHttpContext CreateContext(string path, string origin = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (origin is not null)
        {
            context.Request.Headers["Origin"] = origin;
        }

        return context;
    }

    [Fact]
    public async Task InvokeAsync_NonHubPath_ShouldNotSetCorsHeaders()
    {
        var middleware = CreateMiddleware();
        var context = CreateContext("/api/accounts", "http://localhost:4200");
        var nextCalled = false;

        await middleware.InvokeAsync(
            context,
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            }
        );

        nextCalled.Should().BeTrue();
        context.Response.Headers.ContainsKey("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_HubPath_AllowedOrigin_ShouldSetCorsHeaders()
    {
        var middleware = CreateMiddleware();
        var context = CreateContext("/hub/account", "http://localhost:4200");

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.Headers["Access-Control-Allow-Origin"].ToString().Should().Be("http://localhost:4200");
        context.Response.Headers["Access-Control-Allow-Credentials"].ToString().Should().Be("true");
    }

    [Fact]
    public async Task InvokeAsync_HubPath_DisallowedOrigin_ShouldNotSetCorsHeaders()
    {
        var middleware = CreateMiddleware();
        var context = CreateContext("/hub/account", "https://evil.example.com");

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.Headers.ContainsKey("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_HubPath_NoOriginHeader_ShouldNotSetCorsHeaders()
    {
        var middleware = CreateMiddleware();
        var context = CreateContext("/hub/account");

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.Headers.ContainsKey("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_HubPath_ProductionOrigin_ShouldSetCorsHeaders()
    {
        var middleware = CreateMiddleware("Production");
        var context = CreateContext("/hub/servers", "https://uk-sf.co.uk");

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.Headers["Access-Control-Allow-Origin"].ToString().Should().Be("https://uk-sf.co.uk");
        context.Response.Headers["Access-Control-Allow-Credentials"].ToString().Should().Be("true");
    }

    [Fact]
    public async Task InvokeAsync_HubPath_DevOriginInProduction_ShouldNotSetCorsHeaders()
    {
        var middleware = CreateMiddleware("Production");
        var context = CreateContext("/hub/servers", "http://localhost:4200");

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.Headers.ContainsKey("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_HubPath_UnknownEnvironment_ShouldNotSetCorsHeaders()
    {
        var middleware = CreateMiddleware("Unknown");
        var context = CreateContext("/hub/account", "http://localhost:4200");

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.Headers.ContainsKey("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Theory]
    [InlineData("http://localhost:4200")]
    [InlineData("http://localhost:4300")]
    [InlineData("https://dev.uk-sf.co.uk")]
    [InlineData("https://api-dev.uk-sf.co.uk")]
    public async Task InvokeAsync_HubPath_AllDevelopmentOrigins_ShouldBeAllowed(string origin)
    {
        var middleware = CreateMiddleware();
        var context = CreateContext("/hub/test", origin);

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.Headers["Access-Control-Allow-Origin"].ToString().Should().Be(origin);
    }

    [Theory]
    [InlineData("https://uk-sf.co.uk")]
    [InlineData("https://api.uk-sf.co.uk")]
    public async Task InvokeAsync_HubPath_AllProductionOrigins_ShouldBeAllowed(string origin)
    {
        var middleware = CreateMiddleware("Production");
        var context = CreateContext("/hub/test", origin);

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        context.Response.Headers["Access-Control-Allow-Origin"].ToString().Should().Be(origin);
    }
}
