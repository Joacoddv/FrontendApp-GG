using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;

namespace GastroGestionBlazor.Services.Auth;

/// <summary>
/// DelegatingHandler that attaches Authorization: Bearer to every outbound request
/// made via the "AuthorizedApi" named client. ADR-4.
///
/// DI-cycle note: injects only ILocalStorageService + NavigationManager.
/// Does NOT inject IAuthService or CustomAuthenticationStateProvider — that would
/// create a cycle through IHttpClientFactory. On 401 the handler clears storage
/// directly and navigates to /login; the auth provider detects the missing token
/// on its next GetAuthenticationStateAsync call and returns anonymous state.
/// </summary>
public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly ILocalStorageService _storage;
    private readonly NavigationManager _navigation;

    public BearerTokenHandler(ILocalStorageService storage, NavigationManager navigation)
    {
        _storage = storage;
        _navigation = navigation;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _storage.GetItemAsStringAsync(CustomAuthenticationStateProvider.TokenKey);

        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // Avoid infinite loop: if the 401 came from the login endpoint itself,
            // do nothing — the login page handles its own error display. BAF-10.
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (!path.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
            {
                await _storage.RemoveItemAsync(CustomAuthenticationStateProvider.TokenKey);
                await _storage.RemoveItemAsync(CustomAuthenticationStateProvider.ExpiryKey);

                _navigation.NavigateTo("/login", forceLoad: false);
            }
        }

        return response;
    }
}
