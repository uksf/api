using System;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Auth.Commands;
using UKSF.Api.Auth.Controllers;
using UKSF.Api.Auth.Exceptions;
using UKSF.Api.Auth.Services;
using UKSF.Api.Shared.Exceptions;
using UKSF.Api.Shared.Services;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Auth.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<IHttpContextService> _mockHttpContextService;
        private readonly Mock<ILoginService> _mockLoginService;
        private readonly Mock<IRequestPasswordResetCommand> _mockRequestPasswordResetCommand;
        private readonly Mock<IResetPasswordCommand> _mockResetPasswordCommand;
        private readonly AuthController _subject;
        private readonly string _userId = ObjectId.GenerateNewId().ToString();

        public AuthControllerTests()
        {
            _mockLoginService = new();
            _mockHttpContextService = new();
            _mockRequestPasswordResetCommand = new();
            _mockResetPasswordCommand = new();

            _subject = new(_mockLoginService.Object, _mockHttpContextService.Object, _mockRequestPasswordResetCommand.Object, _mockResetPasswordCommand.Object);
        }

        [Fact]
        public void When_getting_is_user_authed()
        {
            _mockHttpContextService.Setup(x => x.IsUserAuthenticated()).Returns(true);

            var result = _subject.IsUserAuthenticated();

            result.Should().BeTrue();
        }

        [Fact]
        public void When_logging_in()
        {
            _mockLoginService.Setup(x => x.Login("email", "password")).Returns("token");

            var result = _subject.Login(new() { Email = "email", Password = "password" });

            result.Should().Be("token");
        }

        [Fact]
        public void When_refreshing_token()
        {
            _mockHttpContextService.Setup(x => x.GetUserId()).Returns(_userId);
            _mockLoginService.Setup(x => x.RegenerateBearerToken()).Returns("token");

            var result = _subject.RefreshToken();

            result.Should().Be("token");
        }

        [Fact]
        public void When_refreshing_token_fails()
        {
            _mockHttpContextService.Setup(x => x.GetUserId()).Returns(_userId);
            _mockLoginService.Setup(x => x.RegenerateBearerToken()).Returns((string)null);

            Action act = () => _subject.RefreshToken();

            act.Should().Throw<TokenRefreshFailedException>().WithMessageAndStatusCode("Failed to refresh token", 401);
        }

        [Fact]
        public async Task When_requesting_password_reset()
        {
            await _subject.RequestPasswordReset(new() { Email = "email" });

            _mockRequestPasswordResetCommand.Verify(x => x.ExecuteAsync(It.Is<RequestPasswordResetCommandArgs>(m => m.Email == "email")), Times.Once);
        }

        [Fact]
        public async Task When_resetting_password()
        {
            _mockResetPasswordCommand.Setup(x => x.ExecuteAsync(It.Is<ResetPasswordCommandArgs>(m => m.Email == "email" && m.Password == "password" && m.Code == "code")));
            _mockLoginService.Setup(x => x.LoginForPasswordReset("email")).Returns("token");

            var result = await _subject.ResetPassword("code", new() { Email = "email", Password = "password" });

            result.Should().Be("token");
        }

        [Theory, InlineData(null, "password"), InlineData("email", null)]
        public void When_logging_in_with_invalid_credentials(string email, string password)
        {
            Action act = () => _subject.Login(new() { Email = email, Password = password });

            act.Should().Throw<BadRequestException>().WithMessageAndStatusCode("Bad request", 400);
        }
    }
}
