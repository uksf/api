using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fido2NetLib;
using FluentAssertions;
using MongoDB.Bson;
using Moq;
using UKSF.Api.Controllers;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;
using UKSF.Api.Models.Request;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Controllers;

public class PasskeysControllerTests
{
    private readonly DomainAccount _account = new() { Id = ObjectId.GenerateNewId().ToString(), Email = "test@test.com", Password = "hash" };
    private readonly Mock<IPasskeyContext> _mockPasskeyContext = new();
    private readonly Mock<IPasskeyService> _mockPasskeyService = new();
    private readonly List<DomainPasskey> _passkeys = [];
    private readonly PasskeysController _subject;

    public PasskeysControllerTests()
    {
        Mock<IAccountService> mockAccountService = new();
        mockAccountService.Setup(x => x.GetUserAccount()).Returns(_account);
        _mockPasskeyContext.Setup(x => x.Get(It.IsAny<Func<DomainPasskey, bool>>()))
                           .Returns<Func<DomainPasskey, bool>>(predicate => _passkeys.Where(predicate).ToList());

        _subject = new PasskeysController(_mockPasskeyContext.Object, _mockPasskeyService.Object, mockAccountService.Object, new Mock<IUksfLogger>().Object);
    }

    [Fact]
    public void Lists_only_the_users_passkeys()
    {
        _passkeys.Add(Passkey(_account.Id, "Mine"));
        _passkeys.Add(Passkey(ObjectId.GenerateNewId().ToString(), "Theirs"));

        var result = _subject.Get();

        result.HasPassword.Should().BeTrue();
        result.Passkeys.Select(x => x.Name).Should().Equal("Mine");
    }

    [Fact]
    public async Task Registers_a_passkey_for_the_user()
    {
        var credential = new AuthenticatorAttestationRawResponse();
        _mockPasskeyService.Setup(x => x.VerifyRegistration("flow", _account.Email, credential)).ReturnsAsync(Passkey(_account.Id, "Bitwarden"));

        var result = await _subject.Register(new PasskeyRegistrationRequest { FlowId = "flow", Credential = credential });

        result.Name.Should().Be("Bitwarden");
        _mockPasskeyContext.Verify(x => x.Add(It.Is<DomainPasskey>(p => p.AccountId == _account.Id)), Times.Once);
    }

    [Fact]
    public async Task Rejects_a_registration_for_another_account()
    {
        var credential = new AuthenticatorAttestationRawResponse();
        _mockPasskeyService.Setup(x => x.VerifyRegistration("flow", _account.Email, credential))
                           .ReturnsAsync(Passkey(ObjectId.GenerateNewId().ToString(), "Other"));

        var act = () => _subject.Register(new PasskeyRegistrationRequest { FlowId = "flow", Credential = credential });

        await act.Should().ThrowAsync<BadRequestException>();
        _mockPasskeyContext.Verify(x => x.Add(It.IsAny<DomainPasskey>()), Times.Never);
    }

    [Fact]
    public void Creates_profile_options_that_require_verification_and_automatic_options_that_do_not()
    {
        _subject.RegistrationOptions();
        _subject.AutomaticRegistrationOptions();

        _mockPasskeyService.Verify(x => x.CreateRegistrationOptions(_account.Id, _account.Email, It.IsAny<string>(), false), Times.Once);
        _mockPasskeyService.Verify(x => x.CreateRegistrationOptions(_account.Id, _account.Email, It.IsAny<string>(), true), Times.Once);
    }

    [Fact]
    public async Task Deletes_a_passkey()
    {
        var passkey = Passkey(_account.Id, "Mine");
        _passkeys.Add(passkey);

        await _subject.Delete(passkey.Id);

        _mockPasskeyContext.Verify(x => x.Delete(passkey), Times.Once);
    }

    [Fact]
    public async Task Refuses_to_delete_the_only_passkey_without_a_password()
    {
        _account.Password = null;
        var passkey = Passkey(_account.Id, "Mine");
        _passkeys.Add(passkey);

        var act = () => _subject.Delete(passkey.Id);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("Set a password*");
        _mockPasskeyContext.Verify(x => x.Delete(It.IsAny<DomainPasskey>()), Times.Never);
    }

    [Fact]
    public async Task Refuses_to_delete_another_users_passkey()
    {
        var passkey = Passkey(ObjectId.GenerateNewId().ToString(), "Theirs");
        _passkeys.Add(passkey);

        var act = () => _subject.Delete(passkey.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static DomainPasskey Passkey(string accountId, string name) => new() { AccountId = accountId, Name = name, Created = DateTime.UtcNow };
}
