namespace GastroGestionBlazor.Services.Auth;

/// <summary>
/// Body of POST /auth/login and POST /auth/refresh. Carries the access token plus the
/// rotating refresh token and both expiries. Matched to the backend Contracts.Auth.LoginResponse
/// by property name (System.Text.Json ignores casing/order and any extra fields).
/// </summary>
public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    Guid UsuarioId,
    string Rol);
