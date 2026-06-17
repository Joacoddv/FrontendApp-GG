using Blazored.LocalStorage;
using GastroGestionBlazor.Services.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

/// <summary>
/// Blazor WASM auth-state provider backed by localStorage.
/// Kept at project root (no sub-namespace) to preserve the existing class location. ADR-3, ADR-5.
/// </summary>
public sealed class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    // Storage keys shared with BearerTokenHandler and AuthService.
    internal const string TokenKey = "gg_token";
    internal const string ExpiryKey = "gg_token_expiry";

    // Full ClaimTypes.Role URI — PINNED so AuthorizeView Roles/IsInRole resolve. ADR-3.
    private const string RoleUri = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

    private readonly ILocalStorageService _storage;

    public CustomAuthenticationStateProvider(ILocalStorageService storage)
    {
        _storage = storage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _storage.GetItemAsStringAsync(TokenKey);

        if (string.IsNullOrWhiteSpace(token))
            return Anonymous();

        // Check stored expiry first (fast path, no JWT decode needed).
        var expiryStr = await _storage.GetItemAsStringAsync(ExpiryKey);
        if (DateTime.TryParse(expiryStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var storedExpiry)
            && storedExpiry <= DateTime.UtcNow)
        {
            await ClearStorageAsync();
            return Anonymous();
        }

        // Parse JWT and validate its own exp claim.
        var jwtExpiry = JwtPayloadParser.GetExpiryUtc(token);
        if (jwtExpiry.HasValue && jwtExpiry.Value <= DateTime.UtcNow)
        {
            await ClearStorageAsync();
            return Anonymous();
        }

        var claims = JwtPayloadParser.ParseClaims(token).ToList();
        if (!claims.Any())
            return Anonymous();

        // Build identity with roleType pinned to full URI — this is THE critical line.
        // Without it, AuthorizeView Roles="Cocinero" silently returns false. ADR-3.
        var identity = new ClaimsIdentity(
            claims,
            authenticationType: "jwt",
            nameType: ClaimTypes.Email,
            roleType: RoleUri);

        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    /// <summary>
    /// Called after a successful login. Persists token+expiry and notifies subscribers.
    /// </summary>
    public async Task NotifyUserAuthentication(string token, DateTime expiresAtUtc)
    {
        await _storage.SetItemAsStringAsync(TokenKey, token);
        await _storage.SetItemAsStringAsync(ExpiryKey, expiresAtUtc.ToString("O"));

        var claims = JwtPayloadParser.ParseClaims(token).ToList();
        var identity = new ClaimsIdentity(claims, "jwt", ClaimTypes.Email, RoleUri);
        var principal = new ClaimsPrincipal(identity);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(principal)));
    }

    /// <summary>
    /// Called on logout or 401. Clears storage and notifies subscribers.
    /// </summary>
    public async Task NotifyUserLogout()
    {
        await ClearStorageAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous()));
    }

    // ---- private helpers ----

    private static AuthenticationState Anonymous()
        => new(new ClaimsPrincipal(new ClaimsIdentity()));

    private async Task ClearStorageAsync()
    {
        await _storage.RemoveItemAsync(TokenKey);
        await _storage.RemoveItemAsync(ExpiryKey);
    }
}
