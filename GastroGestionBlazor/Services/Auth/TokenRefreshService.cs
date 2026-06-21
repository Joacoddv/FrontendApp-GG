using System.Net.Http.Json;
using Blazored.LocalStorage;

namespace GastroGestionBlazor.Services.Auth;

/// <summary>
/// Owns access/refresh token persistence and the silent rotation against POST /auth/refresh.
/// Shared by BearerTokenHandler (reactive — on a 401) and CustomAuthenticationStateProvider
/// (proactive — when the stored access token has already expired on navigation).
///
/// DI-cycle note: depends only on IHttpClientFactory + ILocalStorageService and only ever
/// resolves the unauthenticated "AuthApi" client (no handler attached), so it never participates
/// in the AuthorizedApi -> BearerTokenHandler cycle. This is also why it does NOT depend on
/// CustomAuthenticationStateProvider; callers decide how to surface a failed refresh.
/// </summary>
public sealed class TokenRefreshService
{
    // Serializes rotation so a burst of callers (or concurrent 401s) triggers a single refresh —
    // rotation would reject the second, already-spent refresh token. WASM is single-threaded but
    // callers still interleave at await points. Static => shared across transient handler instances.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILocalStorageService _storage;

    public TokenRefreshService(IHttpClientFactory httpClientFactory, ILocalStorageService storage)
    {
        _httpClientFactory = httpClientFactory;
        _storage = storage;
    }

    /// <summary>
    /// Exchanges the stored refresh token for a fresh pair and persists it. Returns the new access
    /// token, or null if there is no usable refresh token or the backend rejects it. Callers that
    /// arrive after a peer already refreshed simply pick up the token the peer just stored.
    /// </summary>
    /// <param name="staleAccessToken">
    /// The access token the caller already found unusable. Used to detect a peer refresh: if the
    /// stored token now differs, a rotation already happened and we return it without spending the
    /// refresh token again.
    /// </param>
    public async Task<string?> TryRefreshAsync(string? staleAccessToken, CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var current = await _storage.GetItemAsStringAsync(CustomAuthenticationStateProvider.TokenKey);
            if (!string.IsNullOrWhiteSpace(current) && current != staleAccessToken)
                return current; // a peer refreshed while we waited on the gate

            var refreshToken = await _storage.GetItemAsStringAsync(CustomAuthenticationStateProvider.RefreshTokenKey);
            if (string.IsNullOrWhiteSpace(refreshToken))
                return null;

            var client = _httpClientFactory.CreateClient("AuthApi");
            var response = await client.PostAsJsonAsync(
                "/auth/refresh",
                new RefreshTokenRequest(refreshToken),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var payload = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
            if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
                return null;

            await PersistAsync(payload);
            return payload.AccessToken;
        }
        catch (HttpRequestException)
        {
            // Network failure on refresh — treat as not-refreshed; the caller decides what to do.
            return null;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Persists a token pair + expiries. Single source of truth for the storage keys.</summary>
    public async Task PersistAsync(LoginResponse payload)
    {
        await _storage.SetItemAsStringAsync(CustomAuthenticationStateProvider.TokenKey, payload.AccessToken);
        await _storage.SetItemAsStringAsync(CustomAuthenticationStateProvider.ExpiryKey, payload.ExpiresAtUtc.ToString("O"));
        await _storage.SetItemAsStringAsync(CustomAuthenticationStateProvider.RefreshTokenKey, payload.RefreshToken);
        await _storage.SetItemAsStringAsync(CustomAuthenticationStateProvider.RefreshExpiryKey, payload.RefreshTokenExpiresAtUtc.ToString("O"));
    }

    /// <summary>Removes the whole token pair. Used on logout and on a failed/absent refresh.</summary>
    public async Task ClearAsync()
    {
        await _storage.RemoveItemAsync(CustomAuthenticationStateProvider.TokenKey);
        await _storage.RemoveItemAsync(CustomAuthenticationStateProvider.ExpiryKey);
        await _storage.RemoveItemAsync(CustomAuthenticationStateProvider.RefreshTokenKey);
        await _storage.RemoveItemAsync(CustomAuthenticationStateProvider.RefreshExpiryKey);
    }
}
