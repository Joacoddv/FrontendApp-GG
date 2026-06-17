using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace GastroGestionBlazor.Services.Auth;

/// <summary>
/// Manual JWT payload decoder. Avoids System.IdentityModel.Tokens.Jwt (heavy bundle;
/// server already validated the signature). ADR-2.
/// </summary>
public static class JwtPayloadParser
{
    // Full ClaimTypes.Role URI — must match the key the backend emits and the roleType
    // used when building ClaimsIdentity (ADR-3).
    private const string RoleUri = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

    /// <summary>
    /// Parses claims from a raw JWT string.
    /// Emits role claims under <see cref="ClaimTypes.Role"/> (full URI) regardless of
    /// whether the backend encoded the role under the full URI or the short "role" key.
    /// </summary>
    public static IEnumerable<Claim> ParseClaims(string jwt)
    {
        var payload = DecodePayload(jwt);
        if (payload is null)
            yield break;

        foreach (var kvp in payload)
        {
            // Defensively map short "role" key → full URI so both backends work.
            var claimType = kvp.Key is "role" or RoleUri ? RoleUri : kvp.Key;

            if (kvp.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in kvp.Value.EnumerateArray())
                    yield return new Claim(claimType, element.GetString() ?? string.Empty);
            }
            else
            {
                yield return new Claim(claimType, kvp.Value.ToString());
            }
        }
    }

    /// <summary>
    /// Returns the UTC expiry encoded in the JWT <c>exp</c> claim, or null if absent/invalid.
    /// </summary>
    public static DateTime? GetExpiryUtc(string jwt)
    {
        var payload = DecodePayload(jwt);
        if (payload is null)
            return null;

        if (payload.TryGetValue("exp", out var expElement) &&
            expElement.ValueKind == JsonValueKind.Number &&
            expElement.TryGetInt64(out var exp))
        {
            return DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
        }

        return null;
    }

    // ---- private helpers ----

    private static Dictionary<string, JsonElement>? DecodePayload(string jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
            return null;

        var parts = jwt.Split('.');
        if (parts.Length != 3)
            return null;

        try
        {
            var payloadBytes = Convert.FromBase64String(PadBase64(parts[1]));
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson);
        }
        catch
        {
            return null;
        }
    }

    private static string PadBase64(string base64Url)
    {
        // Base64url uses - and _ instead of + and /; padding with = is omitted.
        var s = base64Url.Replace('-', '+').Replace('_', '/');
        return (s.Length % 4) switch
        {
            2 => s + "==",
            3 => s + "=",
            _ => s
        };
    }
}
