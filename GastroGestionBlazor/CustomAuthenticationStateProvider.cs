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
    internal const string RefreshTokenKey = "gg_refresh_token";
    internal const string RefreshExpiryKey = "gg_refresh_token_expiry";

    // Full ClaimTypes.Role URI — PINNED so AuthorizeView Roles/IsInRole resolve. ADR-3.
    private const string RoleUri = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

    private readonly ILocalStorageService _storage;
    private readonly TokenRefreshService _tokenRefresher;

    public CustomAuthenticationStateProvider(ILocalStorageService storage, TokenRefreshService tokenRefresher)
    {
        _storage = storage;
        _tokenRefresher = tokenRefresher;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _storage.GetItemAsStringAsync(TokenKey);

        // Access token absent or expired? Try a silent rotation off the refresh token before
        // declaring the user anonymous — otherwise an expired access token logs them out on the
        // next navigation even while the refresh token is still valid. TryRefreshAsync returns
        // null fast (no HTTP) when there is no refresh token, so a logged-out browser is cheap.
        if (string.IsNullOrWhiteSpace(token) || await IsAccessTokenExpiredAsync(token))
        {
            token = await _tokenRefresher.TryRefreshAsync(token);
            if (string.IsNullOrWhiteSpace(token))
            {
                await ClearStorageAsync();
                return Anonymous();
            }
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
    /// Called after a successful login. Persists the token pair + expiries and notifies subscribers.
    /// </summary>
    public async Task NotifyUserAuthentication(
        string token,
        DateTime expiresAtUtc,
        string refreshToken,
        DateTime refreshTokenExpiresAtUtc)
    {
        await _storage.SetItemAsStringAsync(TokenKey, token);
        await _storage.SetItemAsStringAsync(ExpiryKey, expiresAtUtc.ToString("O"));
        await _storage.SetItemAsStringAsync(RefreshTokenKey, refreshToken);
        await _storage.SetItemAsStringAsync(RefreshExpiryKey, refreshTokenExpiresAtUtc.ToString("O"));

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

    /// <summary>
    /// True when the stored expiry has passed or the JWT's own exp claim has. The stored expiry
    /// is the fast path (no JWT decode); the exp claim is the backstop if storage drifts.
    /// </summary>
    private async Task<bool> IsAccessTokenExpiredAsync(string token)
    {
        var expiryStr = await _storage.GetItemAsStringAsync(ExpiryKey);
        if (DateTime.TryParse(expiryStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var storedExpiry)
            && storedExpiry <= DateTime.UtcNow)
            return true;

        var jwtExpiry = JwtPayloadParser.GetExpiryUtc(token);
        return jwtExpiry.HasValue && jwtExpiry.Value <= DateTime.UtcNow;
    }

    private Task ClearStorageAsync() => _tokenRefresher.ClearAsync();
}
