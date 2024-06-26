﻿using System.Collections.Generic;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility;

public class HttpContextServiceTests
{
    private readonly HttpContextService _httpContextService;
    private DefaultHttpContext _httpContext;

    public HttpContextServiceTests()
    {
        Mock<IHttpContextAccessor> mockHttpContextAccessor = new();
        Mock<IClock> mockClock = new();
        Mock<IDisplayNameService> mockDisplayNameService = new();

        mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(() => _httpContext);

        _httpContextService = new(mockHttpContextAccessor.Object, mockClock.Object, mockDisplayNameService.Object);
    }

    [Fact]
    public void ShouldGetContextEmail()
    {
        DomainAccount domainAccount = new() { Email = "contact.tim.here@gmail.com" };
        List<Claim> claims = new() { new(ClaimTypes.Email, domainAccount.Email) };
        ClaimsPrincipal contextUser = new(new ClaimsIdentity(claims));
        _httpContext = new() { User = contextUser };

        var subject = _httpContextService.GetUserEmail();

        subject.Should().Be(domainAccount.Email);
    }

    [Fact]
    public void ShouldGetContextId()
    {
        DomainAccount domainAccount = new();
        List<Claim> claims = new() { new(ClaimTypes.Sid, domainAccount.Id, ClaimValueTypes.String) };
        ClaimsPrincipal contextUser = new(new ClaimsIdentity(claims));
        _httpContext = new() { User = contextUser };

        var subject = _httpContextService.GetUserId();

        subject.Should().Be(domainAccount.Id);
    }

    [Fact]
    public void ShouldReturnFalseForInvalidRole()
    {
        List<Claim> claims = new() { new(ClaimTypes.Role, Permissions.Admin) };
        ClaimsPrincipal contextUser = new(new ClaimsIdentity(claims));
        _httpContext = new() { User = contextUser };

        var subject = _httpContextService.UserHasPermission(Permissions.Command);

        subject.Should().BeFalse();
    }

    [Fact]
    public void ShouldReturnTrueForValidRole()
    {
        List<Claim> claims = new() { new(ClaimTypes.Role, Permissions.Admin) };
        ClaimsPrincipal contextUser = new(new ClaimsIdentity(claims));
        _httpContext = new() { User = contextUser };

        var subject = _httpContextService.UserHasPermission(Permissions.Admin);

        subject.Should().BeTrue();
    }
}
