using Fido2NetLib;
using Fido2NetLib.Objects;
using MongoDB.Bson;
using MongoDB.Driver;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Models.Response;

namespace UKSF.Api.Services;

public interface IPasskeyService
{
    PasskeyOptionsResponse<AssertionOptions> CreateLoginOptions();
    Task<DomainPasskey> VerifyLogin(string flowId, AuthenticatorAssertionRawResponse credential);
    PasskeyOptionsResponse<CredentialCreateOptions> CreateRegistrationOptions(string accountId, string email, string displayName, bool automatic = false);
    Task<DomainPasskey> VerifyRegistration(string flowId, string email, AuthenticatorAttestationRawResponse credential);
}

public class PasskeyService(IPasskeyContext passkeyContext, PasskeyFlowStore flowStore, AppSettings appSettings) : IPasskeyService
{
    // Names for the common passkey providers, keyed by AAGUID (https://github.com/passkeydeveloper/passkey-authenticator-aaguids)
    private static readonly Dictionary<Guid, string> ProviderNames = new()
    {
        { Guid.Parse("fbfc3007-154e-4ecc-8c0b-6e020557d7bd"), "iCloud Keychain" },
        { Guid.Parse("ea9b8d66-4d01-1d21-3ce4-b6b48cb575d4"), "Google Password Manager" },
        { Guid.Parse("d548826e-79b4-db40-a3d8-11116f7e8349"), "Bitwarden" },
        { Guid.Parse("bada5566-a7aa-401f-bd96-45619a55120d"), "1Password" },
        { Guid.Parse("08987058-cadc-4b81-b6e1-30de50dcbe96"), "Windows Hello" },
        { Guid.Parse("9ddd1817-af5a-4672-a2b9-3e3dd95000a9"), "Windows Hello" },
        { Guid.Parse("6028b017-b1d4-4c02-b4b3-afcdafc96bb2"), "Windows Hello" }
    };

    private readonly Lazy<Fido2> _fido2 = new(() => CreateFido2(appSettings.WebUrl));

    public PasskeyOptionsResponse<AssertionOptions> CreateLoginOptions()
    {
        var options = _fido2.Value.GetAssertionOptions(new GetAssertionOptionsParams { AllowedCredentials = [], UserVerification = UserVerificationRequirement.Required });
        return new PasskeyOptionsResponse<AssertionOptions> { FlowId = flowStore.Add(new PasskeyFlow(options.ToJson(), null, null)), Options = options };
    }

    public async Task<DomainPasskey> VerifyLogin(string flowId, AuthenticatorAssertionRawResponse credential)
    {
        var flow = flowStore.Take(flowId);
        if (flow.AccountId != null)
        {
            throw new BadRequestException("Passkey sign-in expired, please try again");
        }

        var passkey = passkeyContext.GetSingle(x => x.CredentialId.SequenceEqual(credential.RawId)) ??
                      throw new NotFoundException("This passkey is not registered with UKSF");

        var result = await Verify(() => _fido2.Value.MakeAssertionAsync(
                                          new MakeAssertionParams
                                          {
                                              AssertionResponse = credential,
                                              OriginalOptions = AssertionOptions.FromJson(flow.OptionsJson),
                                              StoredPublicKey = passkey.PublicKey,
                                              StoredSignatureCounter = (uint)passkey.SignCount,
                                              IsUserHandleOwnerOfCredentialIdCallback = (args, _) =>
                                                  Task.FromResult(args.UserHandle.SequenceEqual(passkey.UserHandle))
                                          }
                                      )
        );

        passkey.SignCount = result.SignCount;
        passkey.IsBackedUp = result.IsBackedUp;
        passkey.LastUsed = DateTime.UtcNow;
        await passkeyContext.Update(
            passkey.Id,
            Builders<DomainPasskey>.Update.Set(x => x.SignCount, passkey.SignCount)
                                   .Set(x => x.IsBackedUp, passkey.IsBackedUp)
                                   .Set(x => x.LastUsed, passkey.LastUsed)
        );
        return passkey;
    }

    // Automatic registration is the browser upgrading a saved password silently (conditional create), so there is no user verification to require
    public PasskeyOptionsResponse<CredentialCreateOptions> CreateRegistrationOptions(string accountId, string email, string displayName, bool automatic = false)
    {
        var excludeCredentials = passkeyContext.Get(x => x.AccountId == accountId).Select(x => new PublicKeyCredentialDescriptor(x.CredentialId)).ToList();
        var options = _fido2.Value.RequestNewCredential(
            new RequestNewCredentialParams
            {
                User = new Fido2User { Id = ObjectId.Parse(accountId).ToByteArray(), Name = email, DisplayName = displayName },
                ExcludeCredentials = excludeCredentials,
                AuthenticatorSelection = new AuthenticatorSelection
                {
                    ResidentKey = ResidentKeyRequirement.Required,
                    UserVerification = automatic ? UserVerificationRequirement.Preferred : UserVerificationRequirement.Required
                },
                AttestationPreference = AttestationConveyancePreference.None,
                Extensions = new AuthenticationExtensionsClientInputs { CredProps = true }
            }
        );

        return new PasskeyOptionsResponse<CredentialCreateOptions>
        {
            FlowId = flowStore.Add(new PasskeyFlow(options.ToJson(), accountId, email, automatic)), Options = options
        };
    }

    public async Task<DomainPasskey> VerifyRegistration(string flowId, string email, AuthenticatorAttestationRawResponse credential)
    {
        var flow = flowStore.Take(flowId);
        if (flow.AccountId == null || !string.Equals(flow.Email, email, StringComparison.InvariantCultureIgnoreCase))
        {
            throw new BadRequestException("Passkey registration does not match this account, please try again");
        }

        var registered = await Verify(() => _fido2.Value.MakeNewCredentialAsync(
                                              new MakeNewCredentialParams
                                              {
                                                  AttestationResponse = credential,
                                                  OriginalOptions = CredentialCreateOptions.FromJson(flow.OptionsJson),
                                                  // Only an automatic flow may skip the user presence check, as the WebAuthn Level 3 spec allows for conditional create
                                                  Mediation = flow.Automatic ? CredentialMediationRequirement.Conditional : CredentialMediationRequirement.Optional,
                                                  IsCredentialIdUniqueToUserCallback = (args, _) =>
                                                      Task.FromResult(passkeyContext.GetSingle(x => x.CredentialId.SequenceEqual(args.CredentialId)) == null)
                                              }
                                          )
        );

        return new DomainPasskey
        {
            AccountId = flow.AccountId,
            CredentialId = registered.Id,
            PublicKey = registered.PublicKey,
            UserHandle = registered.User.Id,
            SignCount = registered.SignCount,
            Transports = registered.Transports?.Select(x => x.ToString().ToLowerInvariant()).ToList() ?? [],
            AaGuid = registered.AaGuid.ToString(),
            Name = ProviderNames.GetValueOrDefault(registered.AaGuid, "Passkey"),
            IsBackedUp = registered.IsBackedUp,
            Created = DateTime.UtcNow
        };
    }

    private static async Task<T> Verify<T>(Func<Task<T>> verify)
    {
        try
        {
            return await verify();
        }
        catch (Fido2VerificationException exception)
        {
            throw new BadRequestException($"Passkey verification failed: {exception.Message}");
        }
    }

    private static Fido2 CreateFido2(string webUrl)
    {
        var webUri = new Uri(webUrl);
        return new Fido2(
            new Fido2Configuration
            {
                ServerDomain = webUri.Host,
                ServerName = "UKSF",
                Origins = new HashSet<string> { webUri.GetLeftPart(UriPartial.Authority) },
                Timeout = (uint)TimeSpan.FromMinutes(5).TotalMilliseconds
            },
            null
        );
    }
}
