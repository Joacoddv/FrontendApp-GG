using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;

namespace GastroGestionBlazor.Services.Auth;

/// <summary>
/// DelegatingHandler that attaches Authorization: Bearer to every outbound request
/// made via the "AuthorizedApi" named client. ADR-4.
///
/// On 401 it asks TokenRefreshService to silently renew the access token via POST /auth/refresh
/// (rotation), then replays the original request once. Only when the refresh fails (or there is
/// no refresh token) does it clear storage and bounce to /login.
///
/// DI-cycle note: injects ILocalStorageService, NavigationManager and TokenRefreshService — none
/// of which depend on the "AuthorizedApi" client, so no cycle is formed. It does NOT inject
/// IAuthService or CustomAuthenticationStateProvider, which would cycle through IHttpClientFactory.
/// </summary>
public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly ILocalStorageService _storage;
    private readonly NavigationManager _navigation;
    private readonly TokenRefreshService _tokenRefresher;

    public BearerTokenHandler(
        ILocalStorageService storage,
        NavigationManager navigation,
        TokenRefreshService tokenRefresher)
    {
        _storage = storage;
        _navigation = navigation;
        _tokenRefresher = tokenRefresher;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _storage.GetItemAsStringAsync(CustomAuthenticationStateProvider.TokenKey);

        // Buffer a replayable copy BEFORE sending — a sent request's content may be disposed,
        // so we can't resend the original on retry. Only needed when we carry a token.
        HttpRequestMessage? retryCopy = null;
        if (!string.IsNullOrWhiteSpace(token))
        {
            retryCopy = await CloneAsync(request);
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized || retryCopy is null)
            return response;

        // Never try to refresh off the auth endpoints themselves — that would recurse. BAF-10.
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        if (path.Contains("/auth/login", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/auth/refresh", StringComparison.OrdinalIgnoreCase))
        {
            retryCopy.Dispose();
            return response;
        }

        var newToken = await _tokenRefresher.TryRefreshAsync(token, cancellationToken);
        if (string.IsNullOrWhiteSpace(newToken))
        {
            // Refresh unavailable or rejected — fall back to the original behaviour.
            await _tokenRefresher.ClearAsync();
            _navigation.NavigateTo("/login", forceLoad: false);
            retryCopy.Dispose();
            return response;
        }

        // Replay the original request once, now with the fresh access token.
        response.Dispose();
        retryCopy.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newToken);
        return await base.SendAsync(retryCopy, cancellationToken);
    }

    /// <summary>
    /// Deep-copies a request (method, URI, version, headers and buffered content) so it can be
    /// replayed after a refresh. Content is read into memory because the original is consumed
    /// once sent.
    /// </summary>
    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
        };

        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync();
            var content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            clone.Content = content;
        }

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }
}
