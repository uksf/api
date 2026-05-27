using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using UKSF.Api.Core.Configuration;

namespace UKSF.Api.Core.Services;

public interface ISteamGuardCodeService
{
    /// <summary>Generates the current Steam Guard mobile authenticator code, or null when no shared secret is configured.</summary>
    string GenerateCode();
}

public class SteamGuardCodeService(IOptions<AppSettings> options, IClock clock) : ISteamGuardCodeService
{
    private const string CodeAlphabet = "23456789BCDFGHJKMNPQRTVWXY";
    private const long TimeStepSeconds = 30;
    private readonly string _sharedSecret = options.Value.Secrets.SteamCmd.SharedSecret;

    public string GenerateCode()
    {
        if (string.IsNullOrWhiteSpace(_sharedSecret))
        {
            return null;
        }

        var secret = Convert.FromBase64String(_sharedSecret);
        var unixSeconds = new DateTimeOffset(DateTime.SpecifyKind(clock.UtcNow(), DateTimeKind.Utc)).ToUnixTimeSeconds();

        Span<byte> timeStep = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(timeStep, unixSeconds / TimeStepSeconds);

        Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
        HMACSHA1.HashData(secret, timeStep, hash);

        var offset = hash[^1] & 0x0F;
        var codePoint = ((hash[offset] & 0x7F) << 24) | ((hash[offset + 1] & 0xFF) << 16) | ((hash[offset + 2] & 0xFF) << 8) | (hash[offset + 3] & 0xFF);

        Span<char> code = stackalloc char[5];
        for (var i = 0; i < code.Length; i++)
        {
            code[i] = CodeAlphabet[codePoint % CodeAlphabet.Length];
            codePoint /= CodeAlphabet.Length;
        }

        return new string(code);
    }
}
