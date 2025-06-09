using System.Collections.Generic;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Core.Tests.Services;

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

        _httpContextService = new HttpContextService(mockHttpContextAccessor.Object, mockClock.Object, mockDisplayNameService.Object);
    }

    [Fact]
    public void ShouldGetContextEmail()
    {
        DomainAccount account = new() { Email = "contact.tim.here@gmail.com" };
        List<Claim> claims = [new(ClaimTypes.Email, account.Email)];
        ClaimsPrincipal contextUser = new(new ClaimsIdentity(claims));
        _httpContext = new DefaultHttpContext { User = contextUser };

        var subject = _httpContextService.GetUserEmail();

        subject.Should().Be(account.Email);
    }

    [Fact]
    public void ShouldGetContextId()
    {
        DomainAccount account = new();
        List<Claim> claims = [new(ClaimTypes.Sid, account.Id, ClaimValueTypes.String)];
        ClaimsPrincipal contextUser = new(new ClaimsIdentity(claims));
        _httpContext = new DefaultHttpContext { User = contextUser };

        var subject = _httpContextService.GetUserId();

        subject.Should().Be(account.Id);
    }

    [Fact]
    public void ShouldReturnFalseForInvalidRole()
    {
        List<Claim> claims = [new(ClaimTypes.Role, Permissions.Admin)];
        ClaimsPrincipal contextUser = new(new ClaimsIdentity(claims));
        _httpContext = new DefaultHttpContext { User = contextUser };

        var subject = _httpContextService.UserHasPermission(Permissions.Command);

        subject.Should().BeFalse();
    }

    [Fact]
    public void ShouldReturnTrueForValidRole()
    {
        List<Claim> claims = [new(ClaimTypes.Role, Permissions.Admin)];
        ClaimsPrincipal contextUser = new(new ClaimsIdentity(claims));
        _httpContext = new DefaultHttpContext { User = contextUser };

        var subject = _httpContextService.UserHasPermission(Permissions.Admin);

        subject.Should().BeTrue();
    }
}
