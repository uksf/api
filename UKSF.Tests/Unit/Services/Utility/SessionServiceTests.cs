using System.Collections.Generic;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Services;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility {
    public class SessionServiceTests {
        private readonly HttpContextService _httpContextService;
        private DefaultHttpContext _httpContext;

        public SessionServiceTests() {
            Mock<IHttpContextAccessor> mockHttpContextAccessor = new();

            mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(() => _httpContext);

            _httpContextService = new HttpContextService(mockHttpContextAccessor.Object);
        }

        [Fact]
        public void ShouldGetContextEmail() {
            Account account = new() { Email = "contact.tim.here@gmail.com" };
            List<Claim> claims = new() { new Claim(ClaimTypes.Email, account.Email) };
            ClaimsPrincipal contextUser = new(new ClaimsIdentity(claims));
            _httpContext = new DefaultHttpContext { User = contextUser };

            string subject = _httpContextService.GetUserEmail();

            subject.Should().Be(account.Email);
        }

        [Fact]
        public void ShouldGetContextId() {
            Account account = new();
            List<Claim> claims = new() { new Claim(ClaimTypes.Sid, account.Id, ClaimValueTypes.String) };
            ClaimsPrincipal contextUser = new(new ClaimsIdentity(claims));
            _httpContext = new DefaultHttpContext { User = contextUser };

            string subject = _httpContextService.GetUserId();

            subject.Should().Be(account.Id);
        }

        [Fact]
        public void ShouldReturnFalseForInvalidRole() {
            List<Claim> claims = new() { new Claim(ClaimTypes.Role, Permissions.ADMIN) };
            ClaimsPrincipal contextUser = new(new ClaimsIdentity(claims));
            _httpContext = new DefaultHttpContext { User = contextUser };

            bool subject = _httpContextService.UserHasPermission(Permissions.COMMAND);

            subject.Should().BeFalse();
        }

        [Fact]
        public void ShouldReturnTrueForValidRole() {
            List<Claim> claims = new() { new Claim(ClaimTypes.Role, Permissions.ADMIN) };
            ClaimsPrincipal contextUser = new(new ClaimsIdentity(claims));
            _httpContext = new DefaultHttpContext { User = contextUser };

            bool subject = _httpContextService.UserHasPermission(Permissions.ADMIN);

            subject.Should().BeTrue();
        }
    }
}
