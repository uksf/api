using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Extensions;
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
/// </summary>
public class OcapEmbedTokenService(IAccountService accountService, IDisplayNameService displayNameService, IVariablesService variablesService)
    : IOcapEmbedTokenService
{
    private const string SecretVariable = "OCAP_JWT_SECRET";
    private const string AdminsVariable = "OCAP_ADMIN_STEAM_IDS";
    private static readonly TimeSpan TokenTtl = TimeSpan.FromHours(12);

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
        var displayName = displayNameService.GetDisplayNameWithoutRank(account);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, steamId),
            new("role", role),
            new("steam_name", displayName ?? steamId)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(claims: claims, notBefore: DateTime.UtcNow, expires: DateTime.UtcNow.Add(TokenTtl), signingCredentials: creds);

        // Keep claim type short names (role/sub) — OCAP expects "role", not long URI maps.
        var handler = new JwtSecurityTokenHandler();
        handler.OutboundClaimTypeMap.Clear();
        handler.InboundClaimTypeMap.Clear();
        return new OcapEmbedTokenResponse
        {
            Token = handler.WriteToken(jwt),
            Role = role,
            SteamId = steamId
        };
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
