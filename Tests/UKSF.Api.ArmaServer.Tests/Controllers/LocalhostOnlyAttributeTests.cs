using System.Collections.Generic;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using UKSF.Api.ArmaServer.Controllers;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests.Controllers;

public class LocalhostOnlyAttributeTests
{
    private readonly LocalhostOnlyAttribute _sut = new();

    private static ActionExecutingContext CreateContext(IPAddress remoteIpAddress, bool withForwardedForHeader = false, string forwardedForValue = "1.2.3.4")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = remoteIpAddress;

        if (withForwardedForHeader)
        {
            httpContext.Request.Headers["X-Forwarded-For"] = forwardedForValue;
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(actionContext, [], new Dictionary<string, object>(), null);
    }

    private static ActionExecutingContext CreateContextWithNullIp()
    {
        var httpContext = new DefaultHttpContext();

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(actionContext, [], new Dictionary<string, object>(), null);
    }

    #region Direct localhost connections (no proxy)

    [Fact]
    public void OnActionExecuting_WithLoopbackIpv4_AllowsRequest()
    {
        var context = CreateContext(IPAddress.Loopback);

        _sut.OnActionExecuting(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public void OnActionExecuting_WithLoopbackIpv6_AllowsRequest()
    {
        var context = CreateContext(IPAddress.IPv6Loopback);

        _sut.OnActionExecuting(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public void OnActionExecuting_WithNonLoopbackIp_Returns403()
    {
        var context = CreateContext(IPAddress.Parse("192.168.1.100"));

        _sut.OnActionExecuting(context);

        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public void OnActionExecuting_WithNullRemoteIp_Returns403()
    {
        var context = CreateContextWithNullIp();

        _sut.OnActionExecuting(context);

        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public void OnActionExecuting_WithPublicIp_Returns403()
    {
        var context = CreateContext(IPAddress.Parse("8.8.8.8"));

        _sut.OnActionExecuting(context);

        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(403);
    }

    #endregion

    #region Proxy-routed requests (X-Forwarded-For present)

    [Fact]
    public void OnActionExecuting_WithForwardedForHeader_AndLoopbackIp_Returns403()
    {
        var context = CreateContext(IPAddress.Loopback, withForwardedForHeader: true);

        _sut.OnActionExecuting(context);

        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public void OnActionExecuting_WithForwardedForHeader_SpoofedAsLoopback_Returns403()
    {
        var context = CreateContext(IPAddress.Loopback, withForwardedForHeader: true, forwardedForValue: "127.0.0.1");

        _sut.OnActionExecuting(context);

        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public void OnActionExecuting_WithForwardedForHeader_AndExternalIp_Returns403()
    {
        var context = CreateContext(IPAddress.Parse("8.8.8.8"), withForwardedForHeader: true);

        _sut.OnActionExecuting(context);

        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(403);
    }

    #endregion
}
