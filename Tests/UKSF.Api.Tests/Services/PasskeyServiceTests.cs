using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using Fido2NetLib;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Converters;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.Services;

public class PasskeyServiceTests
{
    // Same serializer settings as the API's MVC pipeline (Program.cs)
    private static readonly JsonSerializerOptions ApiJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new InferredTypeConverter() }
    };

    private readonly string _accountId = ObjectId.GenerateNewId().ToString();
    private readonly SoftwareAuthenticator _authenticator = new("uk-sf.co.uk", "https://uk-sf.co.uk");
    private readonly List<DomainPasskey> _passkeys = [];
    private readonly PasskeyService _subject;

    public PasskeyServiceTests()
    {
        Mock<IPasskeyContext> mockPasskeyContext = new();
        mockPasskeyContext.Setup(x => x.GetSingle(It.IsAny<Func<DomainPasskey, bool>>()))
                          .Returns<Func<DomainPasskey, bool>>(predicate => _passkeys.FirstOrDefault(predicate));
        mockPasskeyContext.Setup(x => x.Get(It.IsAny<Func<DomainPasskey, bool>>()))
                          .Returns<Func<DomainPasskey, bool>>(predicate => _passkeys.Where(predicate).ToList());
        mockPasskeyContext.Setup(x => x.Update(It.IsAny<string>(), It.IsAny<UpdateDefinition<DomainPasskey>>())).Returns(Task.CompletedTask);

        _subject = new PasskeyService(
            mockPasskeyContext.Object,
            new PasskeyFlowStore(TimeProvider.System),
            new AppSettings { WebUrl = "https://uk-sf.co.uk" }
        );
    }

    [Fact]
    public void Registration_options_serialize_to_webauthn_json()
    {
        var json = ToJson(_subject.CreateRegistrationOptions(_accountId, "test@test.com", "Test User"));

        var root = JsonDocument.Parse(json).RootElement;
        root.GetProperty("flowId").GetString().Should().NotBeNullOrEmpty();
        var options = root.GetProperty("options");
        options.GetProperty("rp").GetProperty("id").GetString().Should().Be("uk-sf.co.uk");
        options.GetProperty("challenge").ValueKind.Should().Be(JsonValueKind.String);
        options.GetProperty("user").GetProperty("id").ValueKind.Should().Be(JsonValueKind.String);
        options.GetProperty("user").GetProperty("name").GetString().Should().Be("test@test.com");
        options.GetProperty("authenticatorSelection").GetProperty("residentKey").GetString().Should().Be("required");
        options.GetProperty("authenticatorSelection").GetProperty("userVerification").GetString().Should().Be("required");
        options.GetProperty("pubKeyCredParams").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Registers_then_signs_in_with_a_passkey()
    {
        var passkey = await Register();

        passkey.AccountId.Should().Be(_accountId);
        passkey.CredentialId.Should().Equal(_authenticator.CredentialId);
        passkey.UserHandle.Should().Equal(ObjectId.Parse(_accountId).ToByteArray());
        passkey.Transports.Should().BeEquivalentTo("internal", "hybrid");
        _passkeys.Add(passkey);

        var loginOptions = ToJson(_subject.CreateLoginOptions());
        var signedIn = await _subject.VerifyLogin(FlowId(loginOptions), Assertion(loginOptions));

        signedIn.Id.Should().Be(passkey.Id);
        signedIn.SignCount.Should().Be(1);
        signedIn.LastUsed.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_flow_is_single_use()
    {
        _passkeys.Add(await Register());
        var loginOptions = ToJson(_subject.CreateLoginOptions());
        await _subject.VerifyLogin(FlowId(loginOptions), Assertion(loginOptions));

        var act = () => _subject.VerifyLogin(FlowId(loginOptions), Assertion(loginOptions));

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("Passkey request expired*");
    }

    [Fact]
    public async Task Rejects_a_tampered_signature()
    {
        _passkeys.Add(await Register());
        var loginOptions = ToJson(_subject.CreateLoginOptions());
        var credential = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(
            _authenticator.Assert(OptionsJson(loginOptions), signature => signature.Select((b, i) => i == 10 ? (byte)(b ^ 0xff) : b).ToArray()),
            ApiJson
        );

        var act = () => _subject.VerifyLogin(FlowId(loginOptions), credential);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("Passkey verification failed*");
    }

    [Fact]
    public async Task Rejects_an_unregistered_passkey()
    {
        var loginOptions = ToJson(_subject.CreateLoginOptions());
        _authenticator.Register(OptionsJson(ToJson(_subject.CreateRegistrationOptions(_accountId, "test@test.com", "Test User"))));

        var act = () => _subject.VerifyLogin(FlowId(loginOptions), Assertion(loginOptions));

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("This passkey is not registered with UKSF");
    }

    [Fact]
    public async Task Rejects_registration_for_a_different_email()
    {
        var options = ToJson(_subject.CreateRegistrationOptions(_accountId, "test@test.com", "Test User"));
        var credential = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(_authenticator.Register(OptionsJson(options)), ApiJson);

        var act = () => _subject.VerifyRegistration(FlowId(options), "other@test.com", credential);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("Passkey registration does not match*");
    }

    [Fact]
    public async Task Rejects_a_login_flow_used_for_registration()
    {
        var loginOptions = ToJson(_subject.CreateLoginOptions());
        var registrationOptions = ToJson(_subject.CreateRegistrationOptions(_accountId, "test@test.com", "Test User"));
        var credential = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(
            _authenticator.Register(OptionsJson(registrationOptions)),
            ApiJson
        );

        var act = () => _subject.VerifyRegistration(FlowId(loginOptions), "test@test.com", credential);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("Passkey registration does not match*");
    }

    [Fact]
    public async Task Rejects_a_second_registration_of_the_same_credential()
    {
        _passkeys.Add(await Register());

        var act = () => Register();

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("Passkey verification failed*");
    }

    [Fact]
    public void Automatic_registration_options_prefer_user_verification()
    {
        var json = ToJson(_subject.CreateRegistrationOptions(_accountId, "test@test.com", "Test User", automatic: true));

        var options = JsonDocument.Parse(json).RootElement.GetProperty("options");
        options.GetProperty("authenticatorSelection").GetProperty("userVerification").GetString().Should().Be("preferred");
        options.GetProperty("authenticatorSelection").GetProperty("residentKey").GetString().Should().Be("required");
    }

    [Fact]
    public async Task Automatic_registration_accepts_a_passkey_created_without_user_presence_or_verification()
    {
        var passkey = await Register(automatic: true, flags: 0x40);
        _passkeys.Add(passkey);

        var loginOptions = ToJson(_subject.CreateLoginOptions());
        var signedIn = await _subject.VerifyLogin(FlowId(loginOptions), Assertion(loginOptions));

        signedIn.Id.Should().Be(passkey.Id);
    }

    [Fact]
    public async Task Registration_from_the_profile_still_requires_user_verification()
    {
        var act = () => Register(automatic: false, flags: 0x41);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("Passkey verification failed*");
    }

    [Fact]
    public async Task Registration_from_the_profile_still_requires_user_presence()
    {
        var act = () => Register(automatic: false, flags: 0x44);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("Passkey verification failed*");
    }

    private async Task<DomainPasskey> Register(bool automatic = false, byte flags = 0x45)
    {
        var options = ToJson(_subject.CreateRegistrationOptions(_accountId, "Test@Test.com", "Test User", automatic));
        var credential = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(_authenticator.Register(OptionsJson(options), flags), ApiJson);
        return await _subject.VerifyRegistration(FlowId(options), "test@test.com", credential);
    }

    private AuthenticatorAssertionRawResponse Assertion(string loginOptions)
    {
        return JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(_authenticator.Assert(OptionsJson(loginOptions)), ApiJson);
    }

    private static string ToJson<T>(T value) => JsonSerializer.Serialize(value, ApiJson);

    private static string FlowId(string json) => JsonDocument.Parse(json).RootElement.GetProperty("flowId").GetString();

    private static string OptionsJson(string json) => JsonDocument.Parse(json).RootElement.GetProperty("options").GetRawText();
}
