namespace GastroGestionBlazor.Services.Auth;

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, Guid UsuarioId, string Rol);
