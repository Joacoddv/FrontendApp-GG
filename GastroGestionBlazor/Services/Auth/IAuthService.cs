namespace GastroGestionBlazor.Services.Auth;

public interface IAuthService
{
    Task<bool> LoginAsync(string email, string password);
    Task LogoutAsync();

    /// <summary>Revokes every active session of the current user ("log out everywhere"), then clears local state.</summary>
    Task LogoutAllAsync();
    Task<string?> GetTokenAsync();
    Task<bool> IsAuthenticatedAsync();
}
