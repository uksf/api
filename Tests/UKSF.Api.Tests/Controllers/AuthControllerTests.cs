using System;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Commands;
using UKSF.Api.Controllers;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Services;
using UKSF.Api.Models.Request;
using UKSF.Api.Models.Response;
using UKSF.Api.Services;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Tests.Controllers;

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
        _mockLoginService = new Mock<ILoginService>();
        _mockHttpContextService = new Mock<IHttpContextService>();
        _mockRequestPasswordResetCommand = new Mock<IRequestPasswordResetCommand>();
        _mockResetPasswordCommand = new Mock<IResetPasswordCommand>();

        _subject = new AuthController(
            _mockLoginService.Object,
            _mockHttpContextService.Object,
            _mockRequestPasswordResetCommand.Object,
            _mockResetPasswordCommand.Object
        );
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
        _mockLoginService.Setup(x => x.Login("email", "password")).Returns(new TokenResponse { Token = "token" });

        var result = _subject.Login(new LoginCredentials { Email = "email", Password = "password" });

        result.Token.Should().Be("token");
    }

    [Fact]
    public void When_refreshing_token()
    {
        _mockHttpContextService.Setup(x => x.GetUserId()).Returns(_userId);
        _mockLoginService.Setup(x => x.RegenerateBearerToken()).Returns(new TokenResponse { Token = "token" });

        var result = _subject.RefreshToken();

        result.Token.Should().Be("token");
    }

    [Fact]
    public async Task When_requesting_password_reset()
    {
        await _subject.RequestPasswordReset(new RequestPasswordReset { Email = "email" });

        _mockRequestPasswordResetCommand.Verify(x => x.ExecuteAsync(It.Is<RequestPasswordResetCommandArgs>(m => m.Email == "email")), Times.Once);
    }

    [Fact]
    public async Task When_resetting_password()
    {
        _mockResetPasswordCommand.Setup(x => x.ExecuteAsync(
                                            It.Is<ResetPasswordCommandArgs>(m => m.Email == "email" && m.Password == "password" && m.Code == "code")
                                        )
        );
        _mockLoginService.Setup(x => x.LoginForPasswordReset("email")).Returns(new TokenResponse { Token = "token" });

        var result = await _subject.ResetPassword("code", new LoginCredentials { Email = "email", Password = "password" });

        result.Token.Should().Be("token");
    }

    // Null/empty credential validation is now handled by [Required] attributes on LoginCredentials,
    // enforced by ASP.NET model validation before the action method is called.
}
