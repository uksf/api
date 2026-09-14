using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Services;

public interface IOcapEmbedTokenService
{
    OcapEmbedTokenResponse CreateForCurrentUser();
}

public class OcapEmbedTokenResponse
{
    public string Token { get; set; }
    public string Role { get; set; }
    public string SteamId { get; set; }
}

/// <summary>
/// Mints OCAP-compatible HS256 JWTs so the UKSF AAR iframe can auth without Steam OpenID.
/// Must use the same secret as OCAP setting.json "secret". Claims match OCAP2 web SteamClaims.
/// Signs with raw HMAC-SHA256 (same as golang-jwt) so short secrets still work —
/// Microsoft.IdentityModel rejects HS256 keys under 128 bits.
/// </summary>
public class OcapEmbedTokenService(IAccountService accountService, IDisplayNameService displayNameService, IVariablesService variablesService)
    : IOcapEmbedTokenService
{
    private const string SecretVariable = "OCAP_JWT_SECRET";
    private const string AdminsVariable = "OCAP_ADMIN_STEAM_IDS";
    private static readonly TimeSpan TokenTtl = TimeSpan.FromHours(12);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = null };

    public OcapEmbedTokenResponse CreateForCurrentUser()
    {
        var account = accountService.GetUserAccount() ?? throw new UksfException("Not authenticated", 401);
        var steamId = account.Steamname?.Trim();
        if (string.IsNullOrEmpty(steamId))
        {
            throw new UksfException("Steam ID is not linked on this account", 400);
        }

        var secret = variablesService.GetVariable(SecretVariable).Item?.ToString();
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new UksfException($"{SecretVariable} is not configured", 503);
        }

        var role = IsAdmin(steamId) ? "admin" : "viewer";
        var displayName = displayNameService.GetDisplayNameWithoutRank(account) ?? steamId;
        var now = DateTimeOffset.UtcNow;
        var exp = now.Add(TokenTtl);

        // OCAP SteamClaims: sub, role, steam_name, exp (RegisteredClaims)
        var headerJson = """{"alg":"HS256","typ":"JWT"}""";
        var payloadJson = JsonSerializer.Serialize(
            new Dictionary<string, object>
            {
                ["sub"] = steamId,
                ["role"] = role,
                ["steam_name"] = displayName,
                ["nbf"] = now.ToUnixTimeSeconds(),
                ["exp"] = exp.ToUnixTimeSeconds()
            },
            JsonOpts
        );

        var token = SignHs256(secret, headerJson, payloadJson);
        return new OcapEmbedTokenResponse
        {
            Token = token,
            Role = role,
            SteamId = steamId
        };
    }

    internal static string SignHs256(string secret, string headerJson, string payloadJson)
    {
        var header = Base64Url(Encoding.UTF8.GetBytes(headerJson));
        var payload = Base64Url(Encoding.UTF8.GetBytes(payloadJson));
        var signingInput = $"{header}.{payload}";
        var sig = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signingInput));
        return $"{signingInput}.{Base64Url(sig)}";
    }

    private static string Base64Url(byte[] data)
    {
        return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private bool IsAdmin(string steamId)
    {
        var raw = variablesService.GetVariable(AdminsVariable).Item?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return raw.Split([',', ';', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Any(id => string.Equals(id, steamId, StringComparison.Ordinal));
    }
}
