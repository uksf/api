using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Exceptions;
using UKSF.Api.Extensions;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class LoginServiceTests
{
    private readonly Mock<IAccountContext> _mockAccountContext;
    private readonly Mock<IPermissionsService> _mockPermissionsService;
    private readonly Mock<IHttpContextService> _mockHttpContextService;
    private readonly ILoginService _subject;

    private readonly string _testUserId = ObjectId.GenerateNewId().ToString();
    private readonly string _testEmail = "test@example.com";
    private readonly string _testPassword = "password123";
    private readonly string _testPasswordHash;

    public LoginServiceTests()
    {
        _testPasswordHash = BCrypt.Net.BCrypt.HashPassword(_testPassword);

        _mockAccountContext = new Mock<IAccountContext>();
        _mockPermissionsService = new Mock<IPermissionsService>();
        _mockHttpContextService = new Mock<IHttpContextService>();

        // Set up security key for token generation via reflection (private setter)
        typeof(AuthExtensions).GetProperty(nameof(AuthExtensions.SecurityKey), BindingFlags.Public | BindingFlags.Static)!.SetValue(
            null,
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-key-that-is-definitely-long-enough-for-hmac-sha-256-algorithm-minimum-128-bits!"))
        );

        _subject = new LoginService(_mockAccountContext.Object, _mockPermissionsService.Object, _mockHttpContextService.Object);
    }

    private DomainAccount CreateTestAccount(string id = null, string email = null, string password = null)
    {
        return new DomainAccount
        {
            Id = id ?? _testUserId,
            Email = email ?? _testEmail,
            Password = password ?? _testPasswordHash
        };
    }

    [Fact]
    public void Login_ValidCredentials_ShouldReturnToken()
    {
        var account = CreateTestAccount();
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>())).Returns(account);
        _mockPermissionsService.Setup(x => x.GrantPermissions(account)).Returns(new HashSet<string> { Permissions.Member });

        var result = _subject.Login(_testEmail, _testPassword);

        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Login_ValidCredentials_TokenShouldContainCorrectClaims()
    {
        var account = CreateTestAccount();
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>())).Returns(account);
        _mockPermissionsService.Setup(x => x.GrantPermissions(account)).Returns(new HashSet<string> { Permissions.Member, Permissions.Admin });

        var result = _subject.Login(_testEmail, _testPassword);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.Token);
        token.Claims.First(c => c.Type == ClaimTypes.Email).Value.Should().Be(_testEmail);
        token.Claims.First(c => c.Type == ClaimTypes.Sid).Value.Should().Be(_testUserId);
        token.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).Should().Contain(Permissions.Member).And.Contain(Permissions.Admin);
    }

    [Fact]
    public void Login_NonexistentEmail_ShouldThrowBadRequest()
    {
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>())).Returns((DomainAccount)null);

        var act = () => _subject.Login("nonexistent@example.com", "password");

        act.Should().Throw<BadRequestException>().WithMessage("No user found with that email");
    }

    [Fact]
    public void Login_WrongPassword_ShouldThrowBadRequest()
    {
        var account = CreateTestAccount();
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>())).Returns(account);

        var act = () => _subject.Login(_testEmail, "wrong-password");

        act.Should().Throw<BadRequestException>().WithMessage("Password or email did not match");
    }

    [Fact]
    public void LoginForPasswordReset_ShouldReturnTokenWithoutPasswordCheck()
    {
        var account = CreateTestAccount();
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>())).Returns(account);
        _mockPermissionsService.Setup(x => x.GrantPermissions(account)).Returns(new HashSet<string>());

        var result = _subject.LoginForPasswordReset(_testEmail);

        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void LoginForPasswordReset_NonexistentEmail_ShouldThrowBadRequest()
    {
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>())).Returns((DomainAccount)null);

        var act = () => _subject.LoginForPasswordReset("nonexistent@example.com");

        act.Should().Throw<BadRequestException>();
    }

    [Fact]
    public void LoginForImpersonate_ValidAccount_ShouldReturnTokenWithImpersonationClaim()
    {
        var targetId = ObjectId.GenerateNewId().ToString();
        var targetAccount = CreateTestAccount(id: targetId);
        var impersonatorId = ObjectId.GenerateNewId().ToString();

        _mockAccountContext.Setup(x => x.GetSingle(targetId)).Returns(targetAccount);
        _mockPermissionsService.Setup(x => x.GrantPermissions(targetAccount)).Returns(new HashSet<string>());
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns(impersonatorId);

        var result = _subject.LoginForImpersonate(targetId);

        result.Should().NotBeNull();
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.Token);
        token.Claims.First(c => c.Type == UksfClaimTypes.ImpersonatingUserId).Value.Should().Be(impersonatorId);
    }

    [Fact]
    public void LoginForImpersonate_NonexistentAccount_ShouldThrowBadRequest()
    {
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<string>())).Returns((DomainAccount)null);

        var act = () => _subject.LoginForImpersonate(ObjectId.GenerateNewId().ToString());

        act.Should().Throw<BadRequestException>();
    }

    [Fact]
    public void LoginForImpersonate_TokenShouldHaveShortExpiry()
    {
        var targetId = ObjectId.GenerateNewId().ToString();
        var targetAccount = CreateTestAccount(id: targetId);

        _mockAccountContext.Setup(x => x.GetSingle(targetId)).Returns(targetAccount);
        _mockPermissionsService.Setup(x => x.GrantPermissions(targetAccount)).Returns(new HashSet<string>());
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns(ObjectId.GenerateNewId().ToString());

        var result = _subject.LoginForImpersonate(targetId);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.Token);
        var expiry = token.ValidTo;
        expiry.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void RegenerateBearerToken_ValidUser_ShouldReturnNewToken()
    {
        var account = CreateTestAccount();
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns(_testUserId);
        _mockAccountContext.Setup(x => x.GetSingle(_testUserId)).Returns(account);
        _mockPermissionsService.Setup(x => x.GrantPermissions(account)).Returns(new HashSet<string>());
        _mockHttpContextService.Setup(x => x.HasImpersonationExpired()).Returns(false);

        var result = _subject.RegenerateBearerToken();

        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void RegenerateBearerToken_NonexistentUser_ShouldThrowBadRequest()
    {
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns(_testUserId);
        _mockAccountContext.Setup(x => x.GetSingle(_testUserId)).Returns((DomainAccount)null);

        var act = () => _subject.RegenerateBearerToken();

        act.Should().Throw<BadRequestException>();
    }

    [Fact]
    public void RegenerateBearerToken_ImpersonationExpired_ShouldThrowTokenRefreshFailed()
    {
        var account = CreateTestAccount();
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns(_testUserId);
        _mockAccountContext.Setup(x => x.GetSingle(_testUserId)).Returns(account);
        _mockHttpContextService.Setup(x => x.HasImpersonationExpired()).Returns(true);

        var act = () => _subject.RegenerateBearerToken();

        act.Should().Throw<TokenRefreshFailedException>().WithMessage("Impersonation session expired");
    }

    [Fact]
    public void RegenerateBearerToken_WithActiveImpersonation_ShouldPreserveImpersonatingUserId()
    {
        var account = CreateTestAccount();
        var originalImpersonatorId = ObjectId.GenerateNewId().ToString();

        _mockHttpContextService.Setup(x => x.GetUserId()).Returns(_testUserId);
        _mockAccountContext.Setup(x => x.GetSingle(_testUserId)).Returns(account);
        _mockPermissionsService.Setup(x => x.GrantPermissions(account)).Returns(new HashSet<string>());
        _mockHttpContextService.Setup(x => x.HasImpersonationExpired()).Returns(false);
        _mockHttpContextService.Setup(x => x.GetImpersonatingUserId()).Returns(originalImpersonatorId);

        var result = _subject.RegenerateBearerToken();

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.Token);
        token.Claims.First(c => c.Type == UksfClaimTypes.ImpersonatingUserId).Value.Should().Be(originalImpersonatorId);
    }

    [Fact]
    public void Login_NormalToken_ShouldHave15DayExpiry()
    {
        var account = CreateTestAccount();
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>())).Returns(account);
        _mockPermissionsService.Setup(x => x.GrantPermissions(account)).Returns(new HashSet<string>());

        var result = _subject.Login(_testEmail, _testPassword);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.Token);
        token.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddDays(15), TimeSpan.FromMinutes(1));
    }
}
