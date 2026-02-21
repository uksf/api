using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Moq;
using UKSF.Api.Commands;
using UKSF.Api.Core;
using UKSF.Api.Core.Commands;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using Xunit;

namespace UKSF.Api.Tests.Commands;

public class RequestPasswordResetCommandTests
{
    private readonly Mock<IAccountContext> _mockAccountContext = new();
    private readonly Mock<IConfirmationCodeService> _mockConfirmationCodeService = new();
    private readonly Mock<ISendTemplatedEmailCommand> _mockSendTemplatedEmailCommand = new();
    private readonly Mock<IUksfLogger> _mockLogger = new();
    private readonly Mock<IHostEnvironment> _mockHostEnvironment = new();
    private readonly RequestPasswordResetCommand _subject;

    private readonly List<DomainAccount> _accounts =
    [
        new() { Id = "account1", Email = "user@example.com" }
    ];

    public RequestPasswordResetCommandTests()
    {
        _mockAccountContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainAccount, bool>>()))
                           .Returns((Func<DomainAccount, bool> predicate) => _accounts.FirstOrDefault(predicate));

        _mockConfirmationCodeService.Setup(x => x.CreateConfirmationCode(It.IsAny<string>())).ReturnsAsync("reset-code-123");

        _subject = new RequestPasswordResetCommand(
            _mockAccountContext.Object,
            _mockConfirmationCodeService.Object,
            _mockSendTemplatedEmailCommand.Object,
            _mockLogger.Object,
            _mockHostEnvironment.Object
        );
    }

    [Fact]
    public async Task Sends_email_with_production_url_when_not_development()
    {
        _mockHostEnvironment.Setup(x => x.EnvironmentName).Returns("Production");

        await _subject.ExecuteAsync(new RequestPasswordResetCommandArgs("user@example.com"));

        _mockSendTemplatedEmailCommand.Verify(
            x => x.ExecuteAsync(
                It.Is<SendTemplatedEmailCommandArgs>(a => a.Recipient == "user@example.com" &&
                                                          a.Substitutions["reset"] == "https://uk-sf.co.uk/login?reset=reset-code-123"
                )
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task Sends_email_with_localhost_url_in_development()
    {
        _mockHostEnvironment.Setup(x => x.EnvironmentName).Returns("Development");

        await _subject.ExecuteAsync(new RequestPasswordResetCommandArgs("user@example.com"));

        _mockSendTemplatedEmailCommand.Verify(
            x => x.ExecuteAsync(It.Is<SendTemplatedEmailCommandArgs>(a => a.Substitutions["reset"] == "http://localhost:4200/login?reset=reset-code-123")),
            Times.Once
        );
    }

    [Fact]
    public async Task Silently_returns_when_account_not_found()
    {
        await _subject.ExecuteAsync(new RequestPasswordResetCommandArgs("nonexistent@example.com"));

        _mockConfirmationCodeService.Verify(x => x.CreateConfirmationCode(It.IsAny<string>()), Times.Never);
        _mockSendTemplatedEmailCommand.Verify(x => x.ExecuteAsync(It.IsAny<SendTemplatedEmailCommandArgs>()), Times.Never);
        _mockLogger.Verify(x => x.LogAudit(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Creates_confirmation_code_with_account_id()
    {
        _mockHostEnvironment.Setup(x => x.EnvironmentName).Returns("Production");

        await _subject.ExecuteAsync(new RequestPasswordResetCommandArgs("user@example.com"));

        _mockConfirmationCodeService.Verify(x => x.CreateConfirmationCode("account1"), Times.Once);
    }

    [Fact]
    public async Task Logs_audit_with_account_id()
    {
        _mockHostEnvironment.Setup(x => x.EnvironmentName).Returns("Production");

        await _subject.ExecuteAsync(new RequestPasswordResetCommandArgs("user@example.com"));

        _mockLogger.Verify(x => x.LogAudit("Password reset request made for account1", "account1"), Times.Once);
    }
}
