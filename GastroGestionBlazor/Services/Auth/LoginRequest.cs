namespace GastroGestionBlazor.Services.Auth;

public sealed record LoginRequest(string Email, string Password);

/// <summary>Body for POST /auth/refresh — matches the backend Contracts.Auth.RefrescarTokenRequest.</summary>
public sealed record RefreshTokenRequest(string RefreshToken);
