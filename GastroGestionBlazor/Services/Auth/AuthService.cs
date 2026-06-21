using Blazored.LocalStorage;
using System.Net.Http;
using System.Net.Http.Json;

namespace GastroGestionBlazor.Services.Auth;

/// <summary>
/// Handles login/logout against the backend auth endpoint.
/// Uses the unauthenticated "AuthApi" named client — login requests must NOT carry a Bearer. ADR-4.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILocalStorageService _storage;
    private readonly CustomAuthenticationStateProvider _authProvider;

    public AuthService(
        IHttpClientFactory httpClientFactory,
        ILocalStorageService storage,
        CustomAuthenticationStateProvider authProvider)
    {
        _httpClientFactory = httpClientFactory;
        _storage = storage;
        _authProvider = authProvider;
    }

    /// <inheritdoc />
    public async Task<bool> LoginAsync(string email, string password)
    {
        // Use the unauthenticated client — no Bearer header will be added. ADR-4.
        var client = _httpClientFactory.CreateClient("AuthApi");

        var request = new LoginRequest(email, password);

        var response = await client.PostAsJsonAsync("/auth/login", request);

        if (!response.IsSuccessStatusCode)
            return false;

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (loginResponse is null)
            return false;

        // Persist the token pair and notify auth state (single responsibility in the provider).
        await _authProvider.NotifyUserAuthentication(
            loginResponse.AccessToken,
            loginResponse.ExpiresAtUtc,
            loginResponse.RefreshToken,
            loginResponse.RefreshTokenExpiresAtUtc);

        return true;
    }

    /// <inheritdoc />
    public async Task LogoutAsync()
    {
        await _authProvider.NotifyUserLogout();
    }

    /// <inheritdoc />
    public async Task<string?> GetTokenAsync()
    {
        return await _storage.GetItemAsStringAsync(CustomAuthenticationStateProvider.TokenKey);
    }

    /// <inheritdoc />
    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var expiry = JwtPayloadParser.GetExpiryUtc(token);
        return expiry.HasValue && expiry.Value > DateTime.UtcNow;
    }
}
