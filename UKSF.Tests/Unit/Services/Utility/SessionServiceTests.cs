using System.Collections.Generic;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Services.Utility;
using Xunit;

namespace UKSF.Tests.Unit.Services.Utility {
    public class SessionServiceTests {
        private readonly Mock<IHttpContextAccessor> mockHttpContextAccessor;
        private readonly Mock<IAccountDataService> mockAccountDataService;
        private readonly Mock<IAccountService> mockAccountService;
        private DefaultHttpContext httpContext;

        public SessionServiceTests() {
            mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            mockAccountDataService = new Mock<IAccountDataService>();
            mockAccountService = new Mock<IAccountService>();

            mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(() => httpContext);
            mockAccountService.Setup(x => x.Data).Returns(mockAccountDataService.Object);
        }

        [Fact]
        public void ShouldGetContextId() {
            Account account = new Account();
            List<Claim> claims = new List<Claim> {new Claim(ClaimTypes.Sid, account.id, ClaimValueTypes.String)};
            ClaimsPrincipal contextUser = new ClaimsPrincipal(new ClaimsIdentity(claims));
            httpContext = new DefaultHttpContext {User = contextUser};

            sessionService = new SessionService(mockHttpContextAccessor.Object, mockAccountService.Object);

            string subject = httpContextService.GetUserId();

            subject.Should().Be(account.id);
        }

        [Fact]
        public void ShouldGetContextEmail() {
            Account account = new Account {email = "contact.tim.here@gmail.com"};
            List<Claim> claims = new List<Claim> {new Claim(ClaimTypes.Email, account.email)};
            ClaimsPrincipal contextUser = new ClaimsPrincipal(new ClaimsIdentity(claims));
            httpContext = new DefaultHttpContext {User = contextUser};

            sessionService = new SessionService(mockHttpContextAccessor.Object, mockAccountService.Object);

            string subject = httpContextService.GetUserEmail();

            subject.Should().Be(account.email);
        }

        [Fact]
        public void ShouldGetCorrectAccount() {
            Account account1 = new Account();
            Account account2 = new Account();
            List<Claim> claims = new List<Claim> {new Claim(ClaimTypes.Sid, account2.id)};
            ClaimsPrincipal contextUser = new ClaimsPrincipal(new ClaimsIdentity(claims));
            httpContext = new DefaultHttpContext {User = contextUser};

            mockAccountDataService.Setup(x => x.GetSingle(account1.id)).Returns(account1);
            mockAccountDataService.Setup(x => x.GetSingle(account2.id)).Returns(account2);

            sessionService = new SessionService(mockHttpContextAccessor.Object, mockAccountService.Object);

            Account subject = accountService.GetUserAccount();

            subject.Should().Be(account2);
        }

        [Fact]
        public void ShouldReturnTrueForValidRole() {
            List<Claim> claims = new List<Claim> {new Claim(ClaimTypes.Role, Permissions.ADMIN)};
            ClaimsPrincipal contextUser = new ClaimsPrincipal(new ClaimsIdentity(claims));
            httpContext = new DefaultHttpContext {User = contextUser};

            sessionService = new SessionService(mockHttpContextAccessor.Object, mockAccountService.Object);

            bool subject = sessionService.ContextHasRole(Permissions.ADMIN);

            subject.Should().BeTrue();
        }

        [Fact]
        public void ShouldReturnFalseForInvalidRole() {
            List<Claim> claims = new List<Claim> {new Claim(ClaimTypes.Role, Permissions.ADMIN)};
            ClaimsPrincipal contextUser = new ClaimsPrincipal(new ClaimsIdentity(claims));
            httpContext = new DefaultHttpContext {User = contextUser};

            sessionService = new SessionService(mockHttpContextAccessor.Object, mockAccountService.Object);

            bool subject = sessionService.ContextHasRole(Permissions.COMMAND);

            subject.Should().BeFalse();
        }
    }
}
