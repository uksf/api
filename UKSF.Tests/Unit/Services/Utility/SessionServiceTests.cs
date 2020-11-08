using System.Collections.Generic;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using UKSF.Api.Base;
using UKSF.Api.Base.Services;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services.Data;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility {
    public class SessionServiceTests {
        private readonly HttpContextService httpContextService;
        private DefaultHttpContext httpContext;

        public SessionServiceTests() {
            Mock<IHttpContextAccessor> mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

            mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(() => httpContext);

            httpContextService = new HttpContextService(mockHttpContextAccessor.Object);
        }

        [Fact]
        public void ShouldGetContextEmail() {
            Account account = new Account { email = "contact.tim.here@gmail.com" };
            List<Claim> claims = new List<Claim> { new Claim(ClaimTypes.Email, account.email) };
            ClaimsPrincipal contextUser = new ClaimsPrincipal(new ClaimsIdentity(claims));
            httpContext = new DefaultHttpContext { User = contextUser };

            string subject = httpContextService.GetUserEmail();

            subject.Should().Be(account.email);
        }

        [Fact]
        public void ShouldGetContextId() {
            Account account = new Account();
            List<Claim> claims = new List<Claim> { new Claim(ClaimTypes.Sid, account.id, ClaimValueTypes.String) };
            ClaimsPrincipal contextUser = new ClaimsPrincipal(new ClaimsIdentity(claims));
            httpContext = new DefaultHttpContext { User = contextUser };

            string subject = httpContextService.GetUserId();

            subject.Should().Be(account.id);
        }

        [Fact]
        public void ShouldReturnFalseForInvalidRole() {
            List<Claim> claims = new List<Claim> { new Claim(ClaimTypes.Role, Permissions.ADMIN) };
            ClaimsPrincipal contextUser = new ClaimsPrincipal(new ClaimsIdentity(claims));
            httpContext = new DefaultHttpContext { User = contextUser };

            bool subject = httpContextService.UserHasPermission(Permissions.COMMAND);

            subject.Should().BeFalse();
        }

        [Fact]
        public void ShouldReturnTrueForValidRole() {
            List<Claim> claims = new List<Claim> { new Claim(ClaimTypes.Role, Permissions.ADMIN) };
            ClaimsPrincipal contextUser = new ClaimsPrincipal(new ClaimsIdentity(claims));
            httpContext = new DefaultHttpContext { User = contextUser };

            bool subject = httpContextService.UserHasPermission(Permissions.ADMIN);

            subject.Should().BeTrue();
        }
    }
}
