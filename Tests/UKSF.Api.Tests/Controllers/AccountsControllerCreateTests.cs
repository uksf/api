using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fido2NetLib;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Controllers;
using UKSF.Api.Core;
using UKSF.Api.Core.Commands;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Mappers;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Models.Request;
using UKSF.Api.Models.Response;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Controllers;

public class AccountsControllerCreateTests
{
    private readonly Mock<IAccountContext> _mockAccountContext = new();
    private readonly Mock<ILoginService> _mockLoginService = new();
    private readonly Mock<IPasskeyContext> _mockPasskeyContext = new();
    private readonly Mock<IPasskeyService> _mockPasskeyService = new();
    private readonly AccountsController _subject;

    public AccountsControllerCreateTests()
    {
        _mockAccountContext.Setup(x => x.Get(It.IsAny<Func<DomainAccount, bool>>())).Returns(new List<DomainAccount>());
        _subject = new AccountsController(
            _mockAccountContext.Object,
            new Mock<IConfirmationCodeService>().Object,
            new Mock<IRanksService>().Object,
            new Mock<IAccountService>().Object,
            new Mock<IDisplayNameService>().Object,
            new Mock<IHttpContextService>().Object,
            new Mock<ISendTemplatedEmailCommand>().Object,
            new Mock<IAccountMapper>().Object,
            _mockLoginService.Object,
            _mockPasskeyService.Object,
            _mockPasskeyContext.Object,
            new Mock<IUksfLogger>().Object
        );
    }

    [Fact]
    public async Task Creates_a_password_less_account_with_its_passkey()
    {
        var accountId = ObjectId.GenerateNewId().ToString();
        var request = Request(passkey: new PasskeyRegistrationRequest { FlowId = "flow", Credential = new AuthenticatorAttestationRawResponse() });
        _mockPasskeyService.Setup(x => x.VerifyRegistration("flow", request.Email, request.Passkey.Credential))
                           .ReturnsAsync(new DomainPasskey { AccountId = accountId });
        _mockLoginService.Setup(x => x.LoginForPasskey(accountId)).Returns(new TokenResponse { Token = "token" });

        var result = await _subject.Create(request);

        result.Token.Should().Be("token");
        _mockAccountContext.Verify(x => x.Add(It.Is<DomainAccount>(a => a.Id == accountId && a.Password == null)), Times.Once);
        _mockPasskeyContext.Verify(x => x.Add(It.Is<DomainPasskey>(p => p.AccountId == accountId)), Times.Once);
    }

    [Fact]
    public async Task Removes_the_account_when_its_passkey_cannot_be_stored()
    {
        var accountId = ObjectId.GenerateNewId().ToString();
        var request = Request(passkey: new PasskeyRegistrationRequest { FlowId = "flow", Credential = new AuthenticatorAttestationRawResponse() });
        _mockPasskeyService.Setup(x => x.VerifyRegistration("flow", request.Email, request.Passkey.Credential))
                           .ReturnsAsync(new DomainPasskey { AccountId = accountId });
        _mockPasskeyContext.Setup(x => x.Add(It.IsAny<DomainPasskey>())).ThrowsAsync(new InvalidOperationException("write failed"));

        var act = () => _subject.Create(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _mockAccountContext.Verify(x => x.Delete(accountId), Times.Once);
    }

    [Fact]
    public async Task Rejects_a_request_with_neither_password_nor_passkey()
    {
        var act = () => _subject.Create(Request());

        await act.Should().ThrowAsync<BadRequestException>();
        _mockAccountContext.Verify(x => x.Add(It.IsAny<DomainAccount>()), Times.Never);
    }

    [Fact]
    public async Task Rejects_a_request_with_both_password_and_passkey()
    {
        var act = () => _subject.Create(Request("a-long-password", new PasskeyRegistrationRequest { FlowId = "flow" }));

        await act.Should().ThrowAsync<BadRequestException>();
        _mockPasskeyService.Verify(x => x.VerifyRegistration(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AuthenticatorAttestationRawResponse>()), Times.Never);
    }

    private static CreateAccount Request(string password = null, PasskeyRegistrationRequest passkey = null)
    {
        return new CreateAccount
        {
            Email = "test@test.com",
            FirstName = "test",
            LastName = "user",
            DobDay = 15,
            DobMonth = 2,
            DobYear = 1989,
            Nation = "GB",
            Password = password,
            Passkey = passkey
        };
    }
}
