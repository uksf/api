using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Principal;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Services.Personnel;
using UKSF.Api.Services.Utility;
using Xunit;

namespace UKSF.Tests.Unit.Unit.Services.Utility {
    public class SessionServiceTests {
        private readonly Mock<IHttpContextAccessor> mockHttpContextAccessor;
        private readonly Mock<IAccountDataService> mockAccountDataService;
        private readonly Mock<IAccountService> mockAccountService;
        private ISessionService sessionService;
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

            string subject = sessionService.GetContextId();

            subject.Should().Be(account.id);
        }

        [Fact]
        public void ShouldGetContextEmail() {
            Account account = new Account {email = "contact.tim.here@gmail.com"};
            List<Claim> claims = new List<Claim> {new Claim(ClaimTypes.Email, account.email)};
            ClaimsPrincipal contextUser = new ClaimsPrincipal(new ClaimsIdentity(claims));
            httpContext = new DefaultHttpContext {User = contextUser};

            sessionService = new SessionService(mockHttpContextAccessor.Object, mockAccountService.Object);

            string subject = sessionService.GetContextEmail();

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

            Account subject = sessionService.GetContextAccount();

            subject.Should().Be(account2);
        }

        [Fact]
        public void ShouldReturnTrueForValidRole() {
            List<Claim> claims = new List<Claim> {new Claim(ClaimTypes.Role, RoleDefinitions.ADMIN)};
            ClaimsPrincipal contextUser = new ClaimsPrincipal(new ClaimsIdentity(claims));
            httpContext = new DefaultHttpContext {User = contextUser};

            sessionService = new SessionService(mockHttpContextAccessor.Object, mockAccountService.Object);

            bool subject = sessionService.ContextHasRole(RoleDefinitions.ADMIN);

            subject.Should().BeTrue();
        }

        [Fact]
        public void ShouldReturnFalseForInvalidRole() {
            List<Claim> claims = new List<Claim> {new Claim(ClaimTypes.Role, RoleDefinitions.ADMIN)};
            ClaimsPrincipal contextUser = new ClaimsPrincipal(new ClaimsIdentity(claims));
            httpContext = new DefaultHttpContext {User = contextUser};

            sessionService = new SessionService(mockHttpContextAccessor.Object, mockAccountService.Object);

            bool subject = sessionService.ContextHasRole(RoleDefinitions.COMMAND);

            subject.Should().BeFalse();
        }
    }
}
