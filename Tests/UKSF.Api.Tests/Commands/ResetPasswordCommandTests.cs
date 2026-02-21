using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Moq;
using UKSF.Api.Commands;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Tests.Commands;

public class ResetPasswordCommandTests
{
    private readonly Mock<IAccountContext> _mockAccountContext = new();
    private readonly Mock<IConfirmationCodeService> _mockConfirmationCodeService = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly ResetPasswordCommand _subject;

    private readonly List<DomainAccount> _accounts =
    [
        new() { Id = "account1", Email = "user@example.com" }
    ];

    public ResetPasswordCommandTests()
    {
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>()))
                           .Returns((Func<DomainAccount, bool> predicate) => _accounts.FirstOrDefault(predicate));

        _subject = new ResetPasswordCommand(_mockAccountContext.Object, _mockConfirmationCodeService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Throws_BadRequestException_when_account_not_found()
    {
        var args = new ResetPasswordCommandArgs("nonexistent@example.com", "newpass", "code123");

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _subject.ExecuteAsync(args));

        Assert.Equal("No user found with that email", exception.Message);
    }

    [Fact]
    public async Task Throws_BadRequestException_when_code_is_invalid()
    {
        _mockConfirmationCodeService.Setup(x => x.GetConfirmationCodeValue("badcode")).ReturnsAsync("wrong-account-id");

        var args = new ResetPasswordCommandArgs("user@example.com", "newpass", "badcode");

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _subject.ExecuteAsync(args));

        Assert.Equal("Password reset failed (Invalid code)", exception.Message);
    }

    [Fact]
    public async Task Updates_password_when_code_is_valid()
    {
        _mockConfirmationCodeService.Setup(x => x.GetConfirmationCodeValue("validcode")).ReturnsAsync("account1");

        var args = new ResetPasswordCommandArgs("user@example.com", "newpassword", "validcode");

        await _subject.ExecuteAsync(args);

        _mockAccountContext.Verify(x => x.Update("account1", It.IsAny<Expression<Func<DomainAccount, string>>>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Logs_audit_after_successful_reset()
    {
        _mockConfirmationCodeService.Setup(x => x.GetConfirmationCodeValue("validcode")).ReturnsAsync("account1");

        var args = new ResetPasswordCommandArgs("user@example.com", "newpassword", "validcode");

        await _subject.ExecuteAsync(args);

        _mockLogger.Verify(x => x.LogAudit("Password changed for account1", "account1"), Times.Once);
    }

    [Fact]
    public async Task Calls_GetConfirmationCodeValue_with_provided_code()
    {
        _mockConfirmationCodeService.Setup(x => x.GetConfirmationCodeValue("my-code")).ReturnsAsync("account1");

        var args = new ResetPasswordCommandArgs("user@example.com", "newpassword", "my-code");

        await _subject.ExecuteAsync(args);

        _mockConfirmationCodeService.Verify(x => x.GetConfirmationCodeValue("my-code"), Times.Once);
    }
}
