using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using UKSF.Api.ArmaServer.Middleware;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Middleware;

public class LoopbackOnlyMiddlewareTests
{
    [Theory]
    [InlineData("127.0.0.1", 200)]
    [InlineData("::1", 200)]
    [InlineData("10.0.0.5", 403)]
    public async Task Middleware_filters_by_remote_ip(string remoteIp, int expectedStatus)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remoteIp);
        context.Response.Body = new MemoryStream();

        var middleware = new LoopbackOnlyMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(expectedStatus);
    }

    [Fact]
    public async Task Middleware_rejects_null_remote_ip_address()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = null;
        context.Response.Body = new MemoryStream();

        var middleware = new LoopbackOnlyMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }
}
