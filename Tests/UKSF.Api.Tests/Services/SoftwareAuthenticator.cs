using System;
using System.Buffers.Text;
using System.Formats.Cbor;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UKSF.Api.Tests.Services;

/// <summary>
///     A minimal WebAuthn platform authenticator. It reads options as browser JSON and returns responses as browser JSON,
///     so tests exercise the same wire format the web client sends.
/// </summary>
public sealed class SoftwareAuthenticator(string rpId, string origin)
{
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private uint _counter;

    public byte[] CredentialId { get; } = RandomNumberGenerator.GetBytes(32);
    public byte[] UserHandle { get; private set; }

    // 0x45 = user present, user verified, attested credential data
    public string Register(string optionsJson, byte flags = 0x45)
    {
        var options = JsonNode.Parse(optionsJson)!;
        UserHandle = Base64Url.DecodeFromChars(options["user"]!["id"]!.GetValue<string>());
        var clientData = ClientData("webauthn.create", options["challenge"]!.GetValue<string>());

        var parameters = _key.ExportParameters(false);
        var cose = new CborWriter();
        cose.WriteStartMap(5);
        cose.WriteInt32(1);
        cose.WriteInt32(2);
        cose.WriteInt32(3);
        cose.WriteInt32(-7);
        cose.WriteInt32(-1);
        cose.WriteInt32(1);
        cose.WriteInt32(-2);
        cose.WriteByteString(parameters.Q.X);
        cose.WriteInt32(-3);
        cose.WriteByteString(parameters.Q.Y);
        cose.WriteEndMap();

        byte[] credentialIdLength = [0, (byte)CredentialId.Length];
        var attestedCredentialData = new byte[16].Concat(credentialIdLength).Concat(CredentialId).Concat(cose.Encode()).ToArray();
        var authenticatorData = AuthenticatorData(flags).Concat(attestedCredentialData).ToArray();

        var attestationObject = new CborWriter();
        attestationObject.WriteStartMap(3);
        attestationObject.WriteTextString("fmt");
        attestationObject.WriteTextString("none");
        attestationObject.WriteTextString("attStmt");
        attestationObject.WriteStartMap(0);
        attestationObject.WriteEndMap();
        attestationObject.WriteTextString("authData");
        attestationObject.WriteByteString(authenticatorData);
        attestationObject.WriteEndMap();

        return Credential(
            new JsonObject
            {
                ["clientDataJSON"] = Encode(clientData),
                ["attestationObject"] = Encode(attestationObject.Encode()),
                ["transports"] = new JsonArray("internal", "hybrid")
            }
        );
    }

    public string Assert(string optionsJson, Func<byte[], byte[]> tamperSignature = null)
    {
        var options = JsonNode.Parse(optionsJson)!;
        var clientData = ClientData("webauthn.get", options["challenge"]!.GetValue<string>());
        _counter++;
        var authenticatorData = AuthenticatorData(0x05);
        var signature = _key.SignData(
            authenticatorData.Concat(SHA256.HashData(clientData)).ToArray(),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence
        );

        return Credential(
            new JsonObject
            {
                ["clientDataJSON"] = Encode(clientData),
                ["authenticatorData"] = Encode(authenticatorData),
                ["signature"] = Encode(tamperSignature?.Invoke(signature) ?? signature),
                ["userHandle"] = Encode(UserHandle)
            }
        );
    }

    private string Credential(JsonObject response)
    {
        return new JsonObject
        {
            ["id"] = Encode(CredentialId),
            ["rawId"] = Encode(CredentialId),
            ["type"] = "public-key",
            ["response"] = response,
            ["clientExtensionResults"] = new JsonObject()
        }.ToJsonString();
    }

    private byte[] AuthenticatorData(byte flags)
    {
        byte[] counter = [(byte)(_counter >> 24), (byte)(_counter >> 16), (byte)(_counter >> 8), (byte)_counter];
        return SHA256.HashData(Encoding.UTF8.GetBytes(rpId)).Append(flags).Concat(counter).ToArray();
    }

    private byte[] ClientData(string type, string challenge)
    {
        return JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                type,
                challenge,
                origin,
                crossOrigin = false
            }
        );
    }

    private static string Encode(byte[] bytes) => Base64Url.EncodeToString(bytes);
}
